"""Azure SDK protocol fakes only. These checks establish no live Azure identity, storage or expiry acceptance."""
import hashlib
import importlib.util
import io
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("azure_backup_protocol", ROOT / "scripts/backups/azure_store.py")
transport = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = transport
SPEC.loader.exec_module(transport)
core = transport.core


class Missing(Exception): status_code = 404


class Download:
    def __init__(self, data): self.data = data
    def readall(self): return self.data
    def readinto(self, out): out.write(self.data); return len(self.data)


class Blob:
    def __init__(self, owner, name): self.owner, self.name = owner, name
    def upload_blob(self, data, **options):
        self.owner.calls.append(("upload", self.name, options))
        if self.name in self.owner.data and not options.get("overwrite"): raise RuntimeError("Fake immutable collision")
        self.owner.data[self.name] = data.read() if hasattr(data, "read") else data
    def download_blob(self, **options):
        if self.name not in self.owner.data: raise Missing()
        return Download(self.owner.data[self.name][:options.get("length")])
    def get_blob_properties(self): return SimpleNamespace(size=len(self.owner.data[self.name]))
    def delete_blob(self, **options):
        self.owner.calls.append(("delete", self.name, options))
        if self.name not in self.owner.data: raise Missing()
        del self.owner.data[self.name]


class Container:
    def __init__(self): self.data, self.calls = {}, []
    def get_blob_client(self, name): return Blob(self, name)
    def list_blobs(self, name_starts_with):
        return [SimpleNamespace(name=name) for name in self.data if name.startswith(name_starts_with)]


class AzureProtocol(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.folder = Path(self.temp.name)
        self.local = core.Store(self.folder / "source")
        self.container = Container()
        self.remote = transport.AzureStore(self.container)
        data = b"Fictional encrypted-byte protocol stand-in; not live encryption proof."
        created = datetime(2026, 8, 1, tzinfo=timezone.utc)
        self.record = core.Generation("a" * 32, "monthly", "api", created, core.expires_at("monthly", created), hashlib.sha256(data).hexdigest())
        folder = self.local.directory / self.record.id
        folder.mkdir()
        (folder / "archive.age").write_bytes(data)
        (folder / "generation.json").write_bytes(self.record.encode())

    def tearDown(self): self.temp.cleanup()

    def test_ciphertext_before_commit_marker_and_never_overwrite(self):
        self.remote.publish(self.local, self.record.id)
        names = [name for action, name, _ in self.container.calls if action == "upload"]
        self.assertTrue(names[0].endswith("archive.age"))
        self.assertTrue(names[1].endswith("generation.json"))
        self.assertTrue(all(options["overwrite"] is False and options["validate_content"] is True for _, _, options in self.container.calls))
        self.assertEqual(self.remote.catalogue(), [self.record])
        with self.assertRaises(RuntimeError): self.remote.publish(self.local, self.record.id)

    def test_download_verifies_and_idempotently_preserves_private_catalogue(self):
        self.remote.publish(self.local, self.record.id)
        target = core.Store(self.folder / "target")
        self.assertEqual(self.remote.download(target, self.record.id), self.record)
        self.assertEqual(self.remote.download(target, self.record.id), self.record)
        self.assertEqual(target.records(), [self.record])
        self.assertEqual((target.directory / self.record.id / "archive.age").stat().st_mode & 0o077, 0)

    def test_corrupt_download_never_publishes_a_local_generation(self):
        self.remote.publish(self.local, self.record.id)
        self.container.data[transport.prefix(self.record) + "archive.age"] = b"Fictional changed bytes"
        target = core.Store(self.folder / "target")
        with self.assertRaises(ValueError): self.remote.download(target, self.record.id)
        self.assertEqual(target.records(), [])
        self.assertFalse(any(path.name.startswith(".pending-") for path in target.directory.iterdir()))

    def test_expiry_boundary_and_partial_delete_retry(self):
        self.remote.publish(self.local, self.record.id)
        self.assertEqual(self.remote.expire(datetime(2027, 7, 31, tzinfo=timezone.utc)), 0)
        # Simulate a previous expiry attempt deleting ciphertext before losing the metadata delete response.
        del self.container.data[transport.prefix(self.record) + "archive.age"]
        self.assertEqual(self.remote.expire(datetime(2027, 8, 1, tzinfo=timezone.utc)), 1)
        self.assertEqual(self.remote.expire(datetime(2027, 8, 1, tzinfo=timezone.utc)), 0)
        self.assertTrue(all(options["delete_snapshots"] == "include" for action, _, options in self.container.calls if action == "delete"))

    def test_invalid_metadata_refuses_all_expiry(self):
        self.remote.publish(self.local, self.record.id)
        self.container.data["keycloak/daily/" + "b" * 32 + "/generation.json"] = b"{}"
        before = dict(self.container.data)
        with self.assertRaises(ValueError): self.remote.expire(datetime(2028, 8, 1, tzinfo=timezone.utc))
        self.assertEqual(self.container.data, before)

    def test_uncommitted_ciphertext_is_not_a_complete_generation(self):
        self.container.data[transport.prefix(self.record) + "archive.age"] = b"Fictional partial upload"
        self.assertEqual(self.remote.catalogue(), [])
        self.assertEqual(self.remote.expire(datetime(2028, 8, 1, tzinfo=timezone.utc)), 0)
        self.assertTrue(self.container.data)  # Azure prefix lifecycle backstop handles orphan uploads, not invented metadata.

    def test_remote_path_does_not_relabel_role_or_retention(self):
        self.remote.publish(self.local, self.record.id)
        raw = self.container.data.pop(transport.prefix(self.record) + "generation.json")
        self.container.data["api/daily/" + self.record.id + "/generation.json"] = raw
        with self.assertRaises(ValueError): self.remote.catalogue()


if __name__ == "__main__": unittest.main()
