#!/usr/bin/env python3
"""Owned PostgreSQL/age primitive checks. No application recovery, provider delivery or UAT restore claim."""
import hashlib
import importlib.util
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import uuid

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("swoovr_backup", ROOT / "scripts/backups/backup.py")
backup = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = backup
spec.loader.exec_module(backup)


def run(args, **kwargs):
    return subprocess.run(args, check=True, capture_output=True, **kwargs).stdout


def main():
    age = os.environ.get("SWOOVR_AGE", "age")
    keygen = str(Path(age).with_name("age-keygen"))
    source = "swoovr_backup_" + uuid.uuid4().hex
    targets = []
    created = False
    checks = 0
    def check(value):
        nonlocal checks
        if not value: raise AssertionError("Owned encrypted backup primitive check failed.")
        checks += 1
    def sql(database, statement):
        return run(["docker", "exec", "swoovr-local-postgres-1", "psql", "-U", "swoovr", "-d", database, "-v", "ON_ERROR_STOP=1", "-tAc", statement]).strip()
    values = dict(line.split("=", 1) for line in (ROOT / ".local/docker.env").read_text().splitlines() if "=" in line and not line.startswith("#"))
    try:
        sql("postgres", "CREATE DATABASE " + source)
        created = True
        sql(source, "CREATE TABLE fictional_backup_fixture(id integer PRIMARY KEY,payload text NOT NULL); INSERT INTO fictional_backup_fixture VALUES(1,'Fictional adults only; encrypted PostgreSQL fixture.');")
        with tempfile.TemporaryDirectory(prefix="swoovr-backup-primitive-") as private:
            folder = Path(private)
            os.chmod(folder, 0o700)
            key = folder / "identity"
            wrong = folder / "wrong-identity"
            run([keygen, "--output", str(key)])
            run([keygen, "--output", str(wrong)])
            check(key.stat().st_mode & 0o077 == 0)
            recipient = run([keygen, "--y", str(key)]).decode().strip()
            wrapper = folder / "pg-dump"
            wrapper.write_text('#!/bin/sh\nexec docker exec swoovr-local-postgres-1 pg_dump -U swoovr -d "$PGDATABASE" "$@"\n')
            wrapper.chmod(0o700)
            env = {**os.environ, "PGHOST": "127.0.0.1", "PGPORT": "5433", "PGUSER": "swoovr", "PGPASSWORD": values["SWOOVR_DB_PASSWORD"], "PGDATABASE": source}
            store = backup.Store(folder / "store")
            records = [store.create(tier, "api", recipient, dump=str(wrapper), age=age, env=env) for tier in ["daily", "weekly", "monthly"]]
            check(len(store.records()) == 3)
            check(len({record.sha256 for record in records}) == 3)
            for record in records:
                archive = store.directory / record.id / "archive.age"
                check(archive.read_bytes().startswith(b"age-encryption.org/v1"))
                check(b"Fictional adults only" not in archive.read_bytes())
                check(archive.stat().st_mode & 0o077 == 0)
            target = "swoovr_restore_" + uuid.uuid4().hex
            targets.append(target)
            backup.restore_quarantined(store, records[0].id, key, target, age=age, env={**env, "PGDATABASE": "postgres"})
            check(sql(target, "SELECT payload FROM fictional_backup_fixture WHERE id=1") == b"Fictional adults only; encrypted PostgreSQL fixture.")
            check(sql(target, "SELECT ready FROM swoovr_operations.restore_guard WHERE id=1") == b"f")
            stdin_target = "swoovr_restore_" + uuid.uuid4().hex
            targets.append(stdin_target)
            # Key Vault-style secret injection uses actual age stdin, not a hosted key file or simulated decryptor.
            backup.restore_quarantined(store, records[0].id, None, stdin_target, age=age,
                                       env={**env, "PGDATABASE": "postgres", "SWOOVR_AGE_IDENTITY": key.read_text()})
            check(sql(stdin_target, "SELECT ready FROM swoovr_operations.restore_guard WHERE id=1") == b"f")
            check(sql(stdin_target, "SELECT count(*) FROM fictional_backup_fixture") == b"1")
            refused = False
            try: backup.restore_quarantined(store, records[0].id, key, source, age=age, env=env)
            except ValueError: refused = True
            check(refused)
            failed_target = "swoovr_restore_" + uuid.uuid4().hex
            targets.append(failed_target)
            refused = False
            try: backup.restore_quarantined(store, records[0].id, wrong, failed_target, age=age, env={**env, "PGDATABASE": "postgres"})
            except RuntimeError: refused = True
            check(refused)
            check(sql("postgres", "SELECT count(*) FROM pg_database WHERE datname='" + failed_target + "'") == b"0")
            public_key = folder / "public-identity"
            public_key.write_bytes(key.read_bytes())
            public_key.chmod(0o644)
            refused = False
            try: backup.restore_quarantined(store, records[0].id, public_key, "swoovr_restore_" + uuid.uuid4().hex, age=age, env=env)
            except ValueError: refused = True
            check(refused)
            # Producer failure must never publish a generation, even when encryption itself completes.
            failed_dump = folder / "failed-dump"
            failed_dump.write_text('#!/bin/sh\nprintf "Fictional partial bytes"\nexit 7\n')
            failed_dump.chmod(0o700)
            refused = False
            try: store.create("daily", "api", recipient, dump=str(failed_dump), age=age, env=env)
            except RuntimeError: refused = True
            check(refused and len(store.records()) == 3)
            check(not any(path.name.startswith(".pending-") for path in store.directory.iterdir()))
            # An actual age authentication failure is tested independently of catalogue digest validation.
            damaged = folder / "damaged.age"
            data = bytearray((store.directory / records[1].id / "archive.age").read_bytes())
            data[-1] ^= 1
            damaged.write_bytes(data)
            rejected = subprocess.run([age, "--decrypt", "--identity", str(key), str(damaged)], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode != 0
            check(rejected)
            check(sql(source, "SELECT count(*) FROM fictional_backup_fixture") == b"1")
        print(f"PASS: {checks} owned PostgreSQL/age primitive checks; target remains fenced. Application deletion/hold/expiry reconciliation is not established by this component test.")
    finally:
        for target in targets:
            sql("postgres", "DROP DATABASE IF EXISTS " + target + " WITH (FORCE)")
        if created: sql("postgres", "DROP DATABASE " + source + " WITH (FORCE)")
        print("CLEANUP: owned encrypted-backup source/restore databases and private key/archive directory removed.")


if __name__ == "__main__": main()
