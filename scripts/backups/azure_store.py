#!/usr/bin/env python3
"""Private Azure Blob transport for encrypted generations. Managed identity only; no key/SAS/dev fallback."""
import argparse
from contextlib import contextmanager
from datetime import datetime
import importlib.util
import os
from pathlib import Path
import re
import shutil
import sys

SPEC = importlib.util.spec_from_file_location("swoovr_backup_transport_core", Path(__file__).with_name("backup.py"))
core = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = core
SPEC.loader.exec_module(core)


def prefix(record):
    return record.role + "/" + record.tier + "/" + record.id + "/"


class AzureStore:
    def __init__(self, container):
        self.container = container

    def publish(self, local, generation_id):
        with local.lock():
            record = next((item for item in local.records() if item.id == generation_id), None)
            if record is None: raise ValueError("Validated local encrypted generation required.")
            metadata = {"swoovr_sha256": record.sha256, "swoovr_created": core.stamp(record.created), "swoovr_expires": core.stamp(record.expires)}
            # Metadata is the commit marker: interrupted ciphertext-only uploads are never catalogued as complete.
            with (local.directory / record.id / "archive.age").open("rb") as encrypted:
                self.container.get_blob_client(prefix(record) + "archive.age").upload_blob(encrypted, overwrite=False, metadata=metadata, validate_content=True)
            self.container.get_blob_client(prefix(record) + "generation.json").upload_blob(record.encode(), overwrite=False, metadata=metadata, validate_content=True)
            return record

    def catalogue(self):
        result = []
        for role in ("api", "keycloak"):
            for item in self.container.list_blobs(name_starts_with=role + "/"):
                if not item.name.endswith("/generation.json"): continue
                if not re.fullmatch(r"(?:api|keycloak)/(?:daily|weekly|monthly)/[0-9a-f]{32}/generation\.json", item.name):
                    raise ValueError("Malformed remote backup commit marker.")
                blob = self.container.get_blob_client(item.name)
                if blob.get_blob_properties().size > 4096: raise ValueError("Oversized remote backup metadata.")
                record = core.Generation.decode(blob.download_blob(offset=0, length=4097).readall())
                if item.name != prefix(record) + "generation.json": raise ValueError("Remote generation path/provenance mismatch.")
                result.append(record)
        return result

    def expire(self, now=None):
        records = self.catalogue()  # Validate the complete committed catalogue before any destructive operation.
        due = [record for record in records if record.expired(now or datetime.now(core.UTC))]
        def remove(name):
            try: self.container.get_blob_client(name).delete_blob(delete_snapshots="include")
            except Exception as error:
                if getattr(error, "status_code", None) != 404: raise
        for record in due:
            remove(prefix(record) + "archive.age")
            remove(prefix(record) + "generation.json")
        return len(due)

    def download(self, local, generation_id):
        record = next((item for item in self.catalogue() if item.id == generation_id), None)
        if record is None or record.expired(datetime.now(core.UTC)): raise ValueError("Current committed encrypted generation required.")
        with local.lock():
            existing = next((item for item in local.records() if item.id == generation_id), None)
            if existing is not None:
                if existing != record: raise ValueError("Local/remote provenance mismatch.")
                return existing
            temporary = local.directory / (".pending-" + record.id)
            temporary.mkdir(mode=0o700)
            try:
                archive = temporary / "archive.age"
                fd = os.open(archive, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
                with os.fdopen(fd, "wb") as out:
                    self.container.get_blob_client(prefix(record) + "archive.age").download_blob(max_concurrency=1).readinto(out)
                if core.digest(archive) != record.sha256: raise ValueError("Downloaded ciphertext integrity mismatch.")
                fd = os.open(temporary / "generation.json", os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
                with os.fdopen(fd, "wb") as out: out.write(record.encode())
                temporary.rename(local.directory / record.id)
                return record
            except BaseException:
                if temporary.exists(): shutil.rmtree(temporary)
                raise


@contextmanager
def connect(account_url, container_name, client_id):
    if not re.fullmatch(r"https://[a-z0-9]{3,24}\.blob\.core\.windows\.net", account_url) or not re.fullmatch(r"[a-z0-9][a-z0-9-]{1,61}[a-z0-9]", container_name) or "--" in container_name or not re.fullmatch(r"[0-9a-fA-F-]{36}", client_id):
        raise ValueError("An explicit private Azure account/container/managed identity is required; no SAS/connection string.")
    from azure.identity import ManagedIdentityCredential
    from azure.storage.blob import BlobServiceClient
    with ManagedIdentityCredential(client_id=client_id) as credential, BlobServiceClient(account_url, credential=credential, logging_enable=False) as service:
        yield AzureStore(service.get_container_client(container_name))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--account-url", required=True)
    parser.add_argument("--container", required=True)
    parser.add_argument("--client-id", required=True, help="Explicit user-assigned managed identity client id; not a secret.")
    sub = parser.add_subparsers(dest="command", required=True)
    for name in ("publish", "download"):
        command = sub.add_parser(name)
        command.add_argument("--store", type=Path, required=True)
        command.add_argument("--generation", required=True)
    sub.add_parser("expire")
    sub.add_parser("status")
    args = parser.parse_args()
    with connect(args.account_url, args.container, args.client_id) as remote:
        if args.command in ("publish", "download"):
            getattr(remote, args.command)(core.Store(args.store), args.generation)
            print("Encrypted remote generation operation completed: 1.")
        elif args.command == "expire": print(f"Expired remote encrypted generations removed: {remote.expire()}.")
        else: print(f"Committed remote encrypted generations: {len(remote.catalogue())}.")


if __name__ == "__main__":
    try: main()
    except Exception:
        print("Azure backup operation failed; no fallback or success claim.", file=sys.stderr)
        raise SystemExit(1)
