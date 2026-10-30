#!/usr/bin/env python3
"""Encrypted PostgreSQL generations. Operator-only: credentials remain in PG* environment/secret mounts."""
from __future__ import annotations

import argparse
import calendar
from contextlib import contextmanager
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import tarfile
import uuid

UTC = timezone.utc
SCHEMA = 1


def utc(value: datetime) -> datetime:
    if value.tzinfo is None or value.utcoffset() != timedelta(0):
        raise ValueError("An explicit UTC clock is required.")
    return value


def expires_at(tier: str, created: datetime) -> datetime:
    utc(created)
    if tier == "daily":
        return created + timedelta(days=30)
    if tier == "weekly":
        return created + timedelta(weeks=4)
    if tier == "monthly":
        return created.replace(year=created.year + 1, day=min(created.day, calendar.monthrange(created.year + 1, created.month)[1]))
    raise ValueError("Unsupported backup generation tier.")


def stamp(value: datetime) -> str:
    return utc(value).isoformat().replace("+00:00", "Z")


def parse_stamp(value: str) -> datetime:
    if not isinstance(value, str) or not value.endswith("Z"):
        raise ValueError("Malformed backup UTC timestamp.")
    result = datetime.fromisoformat(value[:-1] + "+00:00")
    if stamp(result) != value:
        raise ValueError("Noncanonical backup UTC timestamp.")
    return utc(result)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate backup metadata field.")
        result[key] = value
    return result


@dataclass(frozen=True)
class Generation:
    id: str
    tier: str
    role: str
    created: datetime
    expires: datetime
    sha256: str
    format: str = "postgres-custom"
    authority_fence: str | None = None

    def __post_init__(self):
        if not isinstance(self.id, str) or not re.fullmatch(r"[0-9a-f]{32}", self.id):
            raise ValueError("Malformed backup generation id.")
        if self.role not in ("api", "keycloak"):
            raise ValueError("Unsupported backup database role.")
        if self.format not in ("postgres-custom", "swoovr-bundle") or self.format == "swoovr-bundle" and self.role != "api":
            raise ValueError("Unsupported backup payload format.")
        if self.authority_fence is not None and (not isinstance(self.authority_fence, str) or not re.fullmatch(r"[0-9a-f]{32}", self.authority_fence)):
            raise ValueError("Malformed reconciliation authority operation.")
        if self.expires != expires_at(self.tier, self.created):
            raise ValueError("Backup expiry does not match its immutable generation clock.")
        if not isinstance(self.sha256, str) or not re.fullmatch(r"[0-9a-f]{64}", self.sha256):
            raise ValueError("Malformed encrypted backup digest.")

    def expired(self, now: datetime) -> bool:
        return utc(now) >= self.expires

    def encode(self) -> bytes:
        return (json.dumps({"schema": SCHEMA, "id": self.id, "tier": self.tier, "role": self.role,
                            "created": stamp(self.created), "expires": stamp(self.expires), "sha256": self.sha256, "format": self.format, "authorityFence": self.authority_fence},
                           sort_keys=True, separators=(",", ":")) + "\n").encode()

    @classmethod
    def decode(cls, data: bytes):
        if len(data) > 4096:
            raise ValueError("Oversized backup metadata.")
        body = json.loads(data, object_pairs_hook=unique_object)
        if not isinstance(body, dict) or set(body) != {"schema", "id", "tier", "role", "created", "expires", "sha256", "format", "authorityFence"} or type(body["schema"]) is not int or body["schema"] != SCHEMA:
            raise ValueError("Unsupported backup metadata schema.")
        return cls(body["id"], body["tier"], body["role"], parse_stamp(body["created"]), parse_stamp(body["expires"]), body["sha256"], body["format"], body["authorityFence"])


def digest(path: Path) -> str:
    result = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


class Store:
    marker = ".swoovr-backup-store.json"

    def __init__(self, directory: Path):
        self.directory = directory.resolve()
        if any((parent / ".git").exists() for parent in (self.directory, *self.directory.parents)):
            raise ValueError("Backup data must live outside every Git checkout, never beside source files.")
        self.directory.mkdir(mode=0o700, parents=True, exist_ok=True)
        if self.directory.stat().st_mode & 0o077:
            raise ValueError("Backup store must be private (mode0700).")
        marker = self.directory / self.marker
        if not marker.exists():
            if any(self.directory.iterdir()):
                raise ValueError("Refusing to adopt a nonempty unmarked backup store.")
            with marker.open("x") as out:
                os.chmod(marker, 0o600)
                json.dump({"purpose": "swoovr-encrypted-backups", "schema": SCHEMA}, out)
        if marker.is_symlink() or json.loads(marker.read_text()) != {"purpose": "swoovr-encrypted-backups", "schema": SCHEMA}:
            raise ValueError("Unrecognized backup store marker.")

    @contextmanager
    def lock(self):
        path = self.directory / ".lock"
        fd = os.open(path, os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW, 0o600)
        with os.fdopen(fd, "w") as handle:
            fcntl.flock(handle, fcntl.LOCK_EX)
            yield

    def records(self) -> list[Generation]:
        records = []
        for directory in sorted(self.directory.iterdir()):
            if directory.name.startswith("."):
                continue
            if directory.is_symlink() or not directory.is_dir() or not re.fullmatch(r"[0-9a-f]{32}", directory.name):
                raise ValueError("Unrecognized backup generation path.")
            metadata = directory / "generation.json"
            archive = directory / "archive.age"
            if metadata.is_symlink() or archive.is_symlink() or not metadata.is_file() or not archive.is_file():
                raise ValueError("Incomplete or symlinked backup generation.")
            record = Generation.decode(metadata.read_bytes())
            if directory.name != record.id or digest(archive) != record.sha256:
                raise ValueError("Encrypted backup missing or changed; no expiry authorised.")
            records.append(record)
        return records

    def recover_staging(self):
        # The store lock proves no active creator/expirer still owns these unpublished/expired directories.
        for path in self.directory.iterdir():
            if re.fullmatch(r"\.(pending|expiring)-[0-9a-f]{32}", path.name):
                if path.is_symlink() or not path.is_dir():
                    raise ValueError("Unexpected backup staging path.")
                shutil.rmtree(path)

    def create(self, tier: str, role: str, recipient: str, *, now: datetime | None = None,
               dump: str = "pg_dump", age: str = "age", env: dict | None = None, format: str = "postgres-custom", authority_fence: str | None = None,
               confirm_source_frozen: bool = False, psql: str = "psql") -> Generation:
        created = utc(now or datetime.now(UTC))
        # Validate trusted operator inputs before contacting PostgreSQL.
        candidate = Generation(uuid.uuid4().hex, tier, role, created, expires_at(tier, created), "0" * 64, format, authority_fence)
        if not recipient.startswith("age1"):
            raise ValueError("An explicit age X25519 recipient is required; no plaintext fallback.")
        if not (os.environ if env is None else env).get("PGDATABASE"):
            raise ValueError("PGDATABASE must be set; connection secrets belong in PG* environment.")
        if authority_fence is not None:
            if not confirm_source_frozen:
                raise ValueError("Current source writes/ingress/jobs must be stopped before reconciliation authority capture.")
            result = checked_tool([psql, "-X", "-tAc", "SELECT EXISTS(SELECT 1 FROM swoovr_operations.restore_guard WHERE id=1 AND NOT ready AND operation_id='" + authority_fence + "' AND role='" + role + "')"], env=env or dict(os.environ))
            if result.strip() != b"t":
                raise ValueError("The source is not marked for this frozen reconciliation operation.")
        with self.lock():
            self.recover_staging()
            partial = self.directory / (".pending-" + candidate.id)
            partial.mkdir(mode=0o700)
            archive = partial / "archive.age"
            metadata = partial / "generation.json"
            producer = consumer = None
            child_env = {key: value for key, value in (os.environ if env is None else env).items() if key != "SWOOVR_AGE_IDENTITY"}
            try:
                fd = os.open(archive, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
                with os.fdopen(fd, "wb") as encrypted, tempfile.TemporaryFile() as private_errors:
                    producer_arguments = [dump] if format == "swoovr-bundle" else [dump, "--format=custom", "--no-owner", "--no-acl", "--exclude-schema=swoovr_operations"]
                    producer = subprocess.Popen(producer_arguments, env=child_env, stdout=subprocess.PIPE, stderr=private_errors)
                    consumer = subprocess.Popen([age, "--encrypt", "--recipient", recipient], stdin=producer.stdout, stdout=encrypted, stderr=private_errors, env=child_env)
                    producer.stdout.close()
                    consumer_status = consumer.wait()
                    producer_status = producer.wait()
                    if producer_status != 0 or consumer_status != 0:
                        raise RuntimeError("Backup dump/encryption failed; no generation published. Private tool diagnostics are not echoed.")
                    encrypted.flush()
                    os.fsync(encrypted.fileno())
                record = Generation(candidate.id, tier, role, created, candidate.expires, digest(archive), format, authority_fence)
                fd = os.open(metadata, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
                with os.fdopen(fd, "wb") as out:
                    out.write(record.encode())
                    out.flush()
                    os.fsync(out.fileno())
                partial.rename(self.directory / candidate.id)
                directory_fd = os.open(self.directory, os.O_RDONLY)
                try:
                    os.fsync(directory_fd)
                finally:
                    os.close(directory_fd)
                return record
            except BaseException:
                for process in (consumer, producer):
                    if process is not None and process.poll() is None:
                        process.terminate()
                        try:
                            process.wait(timeout=5)
                        except subprocess.TimeoutExpired:
                            process.kill()
                            process.wait()
                if partial.exists():
                    shutil.rmtree(partial)
                raise

    def sweep(self, now: datetime | None = None) -> int:
        with self.lock():
            # Validate the complete catalogue first. Missing/invalid provenance cannot cause partial expiry.
            records = self.records()
            due = [record for record in records if record.expired(now or datetime.now(UTC))]
            self.recover_staging()
            for record in due:
                expired = self.directory / (".expiring-" + record.id)
                (self.directory / record.id).rename(expired)
                shutil.rmtree(expired)
            return len(due)


def identity_input(identity: Path | None, settings: dict) -> tuple[list[str], bytes | None]:
    if identity is None:
        secret = settings.get("SWOOVR_AGE_IDENTITY")
        if not secret:
            raise ValueError("A recovery identity must be supplied through Key Vault environment or a private external file.")
        return ["--identity", "-"], secret.encode() + b"\n"
    if identity.is_symlink() or not identity.is_file() or identity.stat().st_mode & 0o077:
        raise ValueError("Restore identity must be a private mode0600 file, never an inline key.")
    return ["--identity", str(identity)], None


def checked_tool(arguments: list[str], *, env: dict, input_data: bytes | None = None, stdout=None):
    sanitized = {key: value for key, value in env.items() if key != "SWOOVR_AGE_IDENTITY"}
    result = subprocess.run(arguments, env=sanitized, input=input_data, stdout=stdout or subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode != 0:
        raise RuntimeError("Backup/restore tool failed; private diagnostics are not echoed.")
    return result.stdout


def extract_database(bundle: Path, destination: Path) -> dict:
    """Validate every bundle member and media digest without extracting caller-controlled paths."""
    with tarfile.open(bundle, "r:") as archive:
        members = archive.getmembers()
        names = [member.name for member in members]
        if len(names) != len(set(names)) or any(not member.isfile() for member in members):
            raise ValueError("Duplicate or nonregular bundle member refused.")
        if "manifest.json" not in names or "database.dump" not in names:
            raise ValueError("Complete database/media bundle is required.")
        info = archive.getmember("manifest.json")
        if info.size > 64 * 1024 * 1024:
            raise ValueError("Oversized backup bundle manifest.")
        manifest = json.loads(archive.extractfile(info).read(), object_pairs_hook=unique_object)
        if not isinstance(manifest, dict) or set(manifest) != {"schema", "role", "capturedAt", "databaseSha256", "objects"} or type(manifest["schema"]) is not int or manifest["schema"] != 1 or manifest["role"] != "api" or not isinstance(manifest["objects"], list):
            raise ValueError("Unsupported backup bundle manifest.")
        if not isinstance(manifest["capturedAt"], str) or len(manifest["capturedAt"]) > 64:
            raise ValueError("Malformed bundle snapshot clock.")
        utc(datetime.fromisoformat(manifest["capturedAt"].replace("Z", "+00:00")))
        expected = {"database.dump", "manifest.json"}
        keys = set()
        for obj in manifest["objects"]:
            if not isinstance(obj, dict) or set(obj) != {"key", "entry", "sha256", "bytes", "contentType"} or not isinstance(obj["key"], str) or not obj["key"] or len(obj["key"]) > 1024 or obj["key"] in keys:
                raise ValueError("Malformed/duplicate backup media reference.")
            entry = "objects/" + hashlib.sha256(obj["key"].encode()).hexdigest() + ".blob"
            if obj["entry"] != entry or not re.fullmatch(r"[0-9a-f]{64}", obj["sha256"]) or type(obj["bytes"]) is not int or not 0 <= obj["bytes"] <= 64 * 1024 * 1024 or not isinstance(obj["contentType"], str) or len(obj["contentType"]) > 255 or "\r" in obj["contentType"] or "\n" in obj["contentType"]:
                raise ValueError("Malformed backup media integrity record.")
            keys.add(obj["key"])
            expected.add(entry)
            member = archive.getmember(entry)
            if member.size != obj["bytes"]:
                raise ValueError("Backup media size changed.")
            result = hashlib.sha256()
            with archive.extractfile(member) as source:
                for block in iter(lambda: source.read(1024 * 1024), b""): result.update(block)
            if result.hexdigest() != obj["sha256"]:
                raise ValueError("Backup media integrity changed.")
        if set(names) != expected:
            raise ValueError("Unexpected backup bundle member.")
        fd = os.open(destination, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
        with os.fdopen(fd, "wb") as out, archive.extractfile("database.dump") as source:
            shutil.copyfileobj(source, out)
        if digest(destination) != manifest["databaseSha256"]:
            raise ValueError("Backup database digest changed.")
        return manifest


def fence_sql(generation: Generation, operation_id: str | None = None) -> str:
    operation_id = operation_id or generation.id
    if not re.fullmatch(r"[0-9a-f]{32}", operation_id):
        raise ValueError("An opaque restore operation id is required.")
    # Operations schema is deliberately excluded from every archive. Replaying data can never import a ready receipt.
    return """CREATE SCHEMA IF NOT EXISTS swoovr_operations;
CREATE TABLE IF NOT EXISTS swoovr_operations.restore_guard (
 id integer PRIMARY KEY CHECK (id=1), ready boolean NOT NULL DEFAULT false,
 generation_id text NOT NULL, operation_id text NOT NULL, role text NOT NULL, restored_at timestamptz NOT NULL,
 authority_generation_id text NULL, authority_sha256 text NULL, authority_payload_sha256 text NULL, authority_captured_at timestamptz NULL,
 authority_replayed_at timestamptz NULL, reconciled_at timestamptz NULL,
 CHECK (NOT ready OR (authority_sha256 IS NOT NULL AND reconciled_at IS NOT NULL)));
INSERT INTO swoovr_operations.restore_guard(id,ready,generation_id,operation_id,role,restored_at)
VALUES(1,false,'%s','%s','%s',now()) ON CONFLICT(id) DO UPDATE SET ready=false,
 generation_id=EXCLUDED.generation_id,operation_id=EXCLUDED.operation_id,role=EXCLUDED.role,restored_at=EXCLUDED.restored_at,
 authority_generation_id=NULL,authority_sha256=NULL,authority_payload_sha256=NULL,authority_captured_at=NULL,authority_replayed_at=NULL,reconciled_at=NULL;
""" % (generation.id, operation_id, generation.role)


def restore_quarantined(store: Store, generation_id: str, identity: Path, target: str, *,
                        age="age", psql="psql", pg_restore="pg_restore", env=None, operation_id: str | None = None) -> Generation:
    settings = dict(os.environ if env is None else env)
    if not re.fullmatch(r"swoovr_restore_[0-9a-f]{32}", target):
        raise ValueError("Restore target must be a fresh owned swoovr_restore_<uuid> database, never the serving database.")
    identity_args, secret_input = identity_input(identity, settings)
    with store.lock():
        records = store.records()
        record = next((item for item in records if item.id == generation_id), None)
        if record is None or record.expired(datetime.now(UTC)):
            raise ValueError("A current validated generation is required for restore.")
        # Authenticate the complete ciphertext before a database sees any restored SQL.
        with tempfile.TemporaryDirectory(prefix="swoovr-restore-private-") as temporary:
            os.chmod(temporary, 0o700)
            plain = Path(temporary) / "authenticated.dump"
            fd = os.open(plain, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
            with os.fdopen(fd, "wb") as out:
                checked_tool([age, "--decrypt", *identity_args, str(store.directory / record.id / "archive.age")], env=settings, input_data=secret_input, stdout=out)
            database_archive = plain
            if record.format == "swoovr-bundle":
                database_archive = Path(temporary) / "database.dump"
                extract_database(plain, database_archive)
            created = False
            try:
                checked_tool([psql, "-X", "--set=ON_ERROR_STOP=1", "--command", "CREATE DATABASE " + target], env=settings)
                created = True
                target_env = {**settings, "PGDATABASE": target}
                checked_tool([psql, "-X", "--set=ON_ERROR_STOP=1"], env=target_env, input_data=fence_sql(record, operation_id).encode())
                checked_tool([pg_restore, "--no-owner", "--no-acl", "--exit-on-error", "--single-transaction", "--dbname", target, str(database_archive)], env=target_env)
                result = checked_tool([psql, "-X", "-tAc", "SELECT ready FROM swoovr_operations.restore_guard WHERE id=1"], env=target_env)
                if result.strip() != b"f":
                    raise RuntimeError("Restored database is not fenced.")
                return record
            except BaseException:
                if created:
                    checked_tool([psql, "-X", "--set=ON_ERROR_STOP=1", "--command", "DROP DATABASE " + target + " WITH (FORCE)"], env=settings)
                raise


@contextmanager
def database_restore_lock(settings: dict, psql: str):
    # A session lock survives the separate pg_restore connections. The matching operator finalizer uses the same key.
    with tempfile.TemporaryFile() as private_errors:
        client = subprocess.Popen([psql, "-X", "-tA", "--set=ON_ERROR_STOP=1"], env={key: value for key, value in settings.items() if key != "SWOOVR_AGE_IDENTITY"},
                                  stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=private_errors)
        try:
            client.stdin.write(b"SELECT pg_advisory_lock(hashtextextended('swoovr:restore',0)); SELECT 'SWOOVR_RESTORE_LOCKED';\n")
            client.stdin.flush()
            while True:
                line = client.stdout.readline()
                if not line:
                    raise RuntimeError("Restore database authority could not be acquired.")
                if line.strip() == b"SWOOVR_RESTORE_LOCKED":
                    break
            yield
        finally:
            try:
                client.stdin.close()
            except OSError:
                pass
            try:
                client.wait(timeout=5)
            except subprocess.TimeoutExpired:
                client.kill()
                client.wait()


def replay_authority(store: Store, generation_id: str, identity: Path, target: str, *, age="age", psql="psql", pg_restore="pg_restore", env=None) -> Generation:
    """Replay a fresh, operation-bound frozen-source snapshot; this never opens serving readiness."""
    settings = dict(os.environ if env is None else env)
    if not re.fullmatch(r"swoovr_restore_[0-9a-f]{32}", target):
        raise ValueError("Reconciliation target must be its owned quarantined database.")
    identity_args, secret_input = identity_input(identity, settings)
    target_env = {**settings, "PGDATABASE": target}
    with store.lock(), database_restore_lock(target_env, psql):
        records = store.records()
        authority = next((item for item in records if item.id == generation_id), None)
        if authority is None or authority.authority_fence is None or authority.expired(datetime.now(UTC)):
            raise ValueError("A current frozen-source authority generation is required, not an old backup alone.")
        if authority.role == "api" and authority.format != "swoovr-bundle":
            raise ValueError("API reconciliation requires current database and referenced media together.")
        raw = checked_tool([psql, "-X", "-tAc", "SELECT row_to_json(g) FROM swoovr_operations.restore_guard g WHERE id=1"], env=target_env)
        state = json.loads(raw)
        original = next((item for item in records if item.id == state["generation_id"]), None)
        now = datetime.now(UTC)
        restored = datetime.fromisoformat(state["restored_at"]).astimezone(UTC)
        if state["ready"] or state["operation_id"] != authority.authority_fence or state["role"] != authority.role or original is None or original.role != authority.role:
            raise ValueError("Authority does not belong to this still-fenced restore operation.")
        if authority.created < original.created or authority.created < restored - timedelta(seconds=5) or not timedelta(seconds=-5) <= now - authority.created <= timedelta(hours=1):
            raise ValueError("Authority is stale, future-dated or predates the quarantined restore.")
        with tempfile.TemporaryDirectory(prefix="swoovr-authority-private-") as temporary:
            os.chmod(temporary, 0o700)
            plain = Path(temporary) / "authenticated.archive"
            fd = os.open(plain, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
            with os.fdopen(fd, "wb") as out:
                checked_tool([age, "--decrypt", *identity_args, str(store.directory / authority.id / "archive.age")], env=settings, input_data=secret_input, stdout=out)
            archive = plain
            if authority.format == "swoovr-bundle":
                archive = Path(temporary) / "database.dump"
                extract_database(plain, archive)
            # The dedicated operations schema is excluded at capture, so the fence/operation is not overwritten.
            checked_tool([pg_restore, "--clean", "--if-exists", "--no-owner", "--no-acl", "--exit-on-error", "--single-transaction", "--dbname", target, str(archive)], env=target_env)
            statement = ("UPDATE swoovr_operations.restore_guard SET authority_generation_id='" + authority.id
                         + "',authority_sha256='" + authority.sha256 + "',authority_payload_sha256='" + digest(plain) + "',authority_captured_at='" + stamp(authority.created)
                         + "',authority_replayed_at=now() WHERE id=1 AND NOT ready AND operation_id='" + authority.authority_fence + "' RETURNING id")
            accepted = checked_tool([psql, "-X", "-tA", "--set=ON_ERROR_STOP=1", "--command", statement], env=target_env)
            if not accepted.strip().startswith(b"1"):
                raise RuntimeError("Replayed authority was not recorded against the fenced operation.")
            # Media remains private inside this authenticated bundle until the operator importer validates its current DB references.
            return authority


def complete_api_reconciliation(store: Store, generation_id: str, identity: Path, target: str, api_command: list[str], *, age="age", env=None) -> Generation:
    settings = dict(os.environ if env is None else env)
    identity_args, secret_input = identity_input(identity, settings)
    authority = replay_authority(store, generation_id, identity, target, age=age, env=settings)
    if authority.role != "api" or authority.format != "swoovr-bundle":
        raise ValueError("Complete API reconciliation requires its current database/media authority bundle.")
    with store.lock(), tempfile.TemporaryDirectory(prefix="swoovr-reconcile-private-") as temporary:
        os.chmod(temporary, 0o700)
        plain = Path(temporary) / "authenticated.bundle"
        fd = os.open(plain, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
        with os.fdopen(fd, "wb") as out:
            checked_tool([age, "--decrypt", *identity_args, str(store.directory / authority.id / "archive.age")], env=settings, input_data=secret_input, stdout=out)
        checked_tool([*api_command, "backups", "reconcile", target, str(plain), authority.sha256], env=settings)
        row = checked_tool(["psql", "-X", "-tAc", "SELECT ready AND authority_sha256='" + authority.sha256 + "' AND reconciled_at>=restored_at FROM swoovr_operations.restore_guard WHERE id=1"], env={**settings, "PGDATABASE": target})
        if row.strip() != b"t":
            raise RuntimeError("API serving receipt could not be confirmed after reconciliation.")
    return authority


def finalize_keycloak(store: Store, authority_id: str, target: str, paired_api: str, *, psql="psql", env=None) -> None:
    """Prepare the paired identity DB only after the same operation's real API reconciliation receipt exists."""
    settings = dict(os.environ if env is None else env)
    if not all(re.fullmatch(r"swoovr_restore_[0-9a-f]{32}", value) for value in (target, paired_api)) or target == paired_api:
        raise ValueError("Distinct owned API/Keycloak restore targets are required.")
    with store.lock(), database_restore_lock({**settings, "PGDATABASE": target}, psql):
        authority = next((record for record in store.records() if record.id == authority_id), None)
        if authority is None or authority.role != "keycloak" or authority.authority_fence is None:
            raise ValueError("Current paired Keycloak authority is required.")
        api_raw = checked_tool([psql, "-X", "-tAc", "SELECT row_to_json(g) FROM swoovr_operations.restore_guard g WHERE id=1"], env={**settings, "PGDATABASE": paired_api})
        api = json.loads(api_raw)
        if not api["ready"] or api["role"] != "api" or api["operation_id"] != authority.authority_fence or not re.fullmatch(r"[0-9a-f]{64}", api["authority_sha256"] or ""):
            raise ValueError("Matching completed API data/media/expiry/session reconciliation is required first.")
        if not timedelta(seconds=-5) <= datetime.now(UTC) - authority.created <= timedelta(hours=1):
            raise ValueError("Keycloak reconciliation authority is stale.")
        statement = ("UPDATE swoovr_operations.restore_guard SET ready=true,reconciled_at=now() WHERE id=1 AND NOT ready AND role='keycloak' AND operation_id='"
                     + authority.authority_fence + "' AND authority_sha256='" + authority.sha256 + "' AND authority_replayed_at IS NOT NULL RETURNING id")
        result = checked_tool([psql, "-X", "-tA", "--set=ON_ERROR_STOP=1", "--command", statement], env={**settings, "PGDATABASE": target})
        if not result.strip().startswith(b"1"):
            raise ValueError("Paired identity readiness did not match its replayed authority.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--store", type=Path, required=True)
    sub = parser.add_subparsers(dest="command", required=True)
    create = sub.add_parser("create")
    create.add_argument("--tier", choices=("daily", "weekly", "monthly"), required=True)
    create.add_argument("--role", choices=("api", "keycloak"), required=True)
    create.add_argument("--recipient", required=True)
    create.add_argument("--authority-fence")
    create.add_argument("--confirm-source-frozen", action="store_true")
    create.add_argument("--bundle-producer", help="Trusted operator wrapper for the API backups bundle command; required for complete API backups.")
    fence = sub.add_parser("fence-source")
    fence.add_argument("--generation", required=True)
    fence.add_argument("--operation", required=True)
    fence.add_argument("--confirm-source-stopped", action="store_true", required=True)
    paired = sub.add_parser("complete-keycloak")
    paired.add_argument("--generation", required=True)
    paired.add_argument("--target", required=True)
    paired.add_argument("--paired-api", required=True)
    sub.add_parser("sweep")
    sub.add_parser("status")
    restore = sub.add_parser("restore-quarantined")
    restore.add_argument("--generation", required=True)
    restore.add_argument("--identity", type=Path, help="Optional private local file; hosted jobs use the Key Vault-referenced SWOOVR_AGE_IDENTITY via stdin.")
    restore.add_argument("--target", required=True)
    restore.add_argument("--operation")
    replay = sub.add_parser("replay-authority")
    replay.add_argument("--generation", required=True)
    replay.add_argument("--identity", type=Path)
    replay.add_argument("--target", required=True)
    finish = sub.add_parser("complete-api")
    finish.add_argument("--generation", required=True)
    finish.add_argument("--identity", type=Path)
    finish.add_argument("--target", required=True)
    finish.add_argument("--api-dll", type=Path, required=True)
    args = parser.parse_args()
    store = Store(args.store)
    if args.command == "create":
        if args.role == "api" and not args.bundle_producer:
            raise ValueError("API backups require their database and referenced private media bundle.")
        if args.role != "api" and args.bundle_producer:
            raise ValueError("Keycloak uses its database archive format.")
        store.create(args.tier, args.role, args.recipient, dump=args.bundle_producer or os.environ.get("SWOOVR_PG_DUMP", "pg_dump"),
                     age=os.environ.get("SWOOVR_AGE", "age"), format="swoovr-bundle" if args.bundle_producer else "postgres-custom", authority_fence=args.authority_fence,
                     confirm_source_frozen=args.confirm_source_frozen)
        print("Encrypted backup generation published: 1.")
    elif args.command == "fence-source":
        with store.lock():
            record = next((item for item in store.records() if item.id == args.generation), None)
            if record is None or record.expired(datetime.now(UTC)):
                raise ValueError("A current validated source generation is required.")
            checked_tool(["psql", "-X", "--set=ON_ERROR_STOP=1"], env=dict(os.environ), input_data=fence_sql(record, args.operation).encode())
        print("Source database fenced. Keep its hosts/jobs stopped until recovery or explicit operator rollback.")
    elif args.command == "complete-keycloak":
        finalize_keycloak(store, args.generation, args.target, args.paired_api)
        print("Paired Keycloak receipt confirmed. Ingress promotion remains an explicit operator action.")
    elif args.command == "sweep":
        print(f"Expired encrypted generations removed: {store.sweep()}.")
    elif args.command == "complete-api":
        complete_api_reconciliation(store, args.generation, args.identity, args.target, ["dotnet", str(args.api_dll.resolve())], age=os.environ.get("SWOOVR_AGE", "age"))
        print("API data/media/expiry/session reconciliation confirmed. Paired Keycloak and ingress promotion remain separate operator gates.")
    elif args.command == "replay-authority":
        replay_authority(store, args.generation, args.identity, args.target, age=os.environ.get("SWOOVR_AGE", "age"))
        print("Current frozen-source data replayed. Readiness remains fenced pending media/retention/session reconciliation.")
    elif args.command == "restore-quarantined":
        restore_quarantined(store, args.generation, args.identity, args.target, age=os.environ.get("SWOOVR_AGE", "age"), operation_id=args.operation)
        print("Quarantined restore completed. Readiness remains fenced; no serving approval.")
    else:
        with store.lock():
            records = store.records()
        print(f"Validated encrypted backup generations: {len(records)}.")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        # Never echo connection strings, SQL, key material or provider diagnostics into operator logs.
        print("Backup operation failed; no success or restore-readiness claim.", file=__import__("sys").stderr)
        raise SystemExit(1)
