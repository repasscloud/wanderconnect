import importlib.util
import sys
import hashlib
import json
import tempfile
import io
import tarfile
import unittest
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("swoovr_backup", ROOT / "scripts/backups/backup.py")
backup = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = backup
SPEC.loader.exec_module(backup)

class RetentionPolicy(unittest.TestCase):
    def test_daily_thirty_days(self):
        self.assertEqual(backup.expires_at("daily", datetime(2026, 10, 30, tzinfo=timezone.utc)), datetime(2026, 11, 29, tzinfo=timezone.utc))

    def test_weekly_four_weeks(self):
        self.assertEqual(backup.expires_at("weekly", datetime(2026, 10, 30, tzinfo=timezone.utc)), datetime(2026, 11, 27, tzinfo=timezone.utc))

    def test_monthly_twelve_calendar_months(self):
        self.assertEqual(backup.expires_at("monthly", datetime(2023, 3, 1, tzinfo=timezone.utc)), datetime(2024, 3, 1, tzinfo=timezone.utc))

    def test_leap_day_clamps_to_calendar_month(self):
        self.assertEqual(backup.expires_at("monthly", datetime(2024, 2, 29, tzinfo=timezone.utc)), datetime(2025, 2, 28, tzinfo=timezone.utc))

    def test_unknown_tier_refused(self):
        with self.assertRaises(ValueError): backup.expires_at("forever", datetime.now(timezone.utc))

    def test_naive_time_refused(self):
        with self.assertRaises(ValueError): backup.expires_at("daily", datetime(2026, 10, 30))

    def test_expiry_boundary_and_no_access_refresh(self):
        created = datetime(2026, 10, 30, tzinfo=timezone.utc)
        record = backup.Generation("a" * 32, "daily", "api", created, backup.expires_at("daily", created), "b" * 64)
        self.assertFalse(record.expired(datetime(2026, 11, 28, 23, 59, 59, tzinfo=timezone.utc)))
        self.assertTrue(record.expired(datetime(2026, 11, 29, tzinfo=timezone.utc)))

    def test_arbitrary_paths_cannot_be_generation_ids(self):
        with self.assertRaises(ValueError): backup.Generation("../outside", "daily", "api", datetime.now(timezone.utc), datetime.now(timezone.utc), "b" * 64)

    def test_forged_early_expiry_refused(self):
        now = datetime(2026, 10, 30, tzinfo=timezone.utc)
        with self.assertRaises(ValueError): backup.Generation("a" * 32, "daily", "api", now, now, "b" * 64)

class CataloguePolicy(unittest.TestCase):
    # Catalogue fixtures are explicit synthetic bytes; these checks make no encryption/restore claim.
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.store = backup.Store(self.root / "private-store")
        self.now = datetime(2026, 10, 30, tzinfo=timezone.utc)

    def tearDown(self): self.temp.cleanup()

    def generation(self, tier="daily", created=None):
        created = created or self.now
        data = b"Fictional catalogue-only encrypted-byte stand-in; not encryption evidence."
        record = backup.Generation(__import__("uuid").uuid4().hex, tier, "api", created, backup.expires_at(tier, created), hashlib.sha256(data).hexdigest())
        folder = self.store.directory / record.id
        folder.mkdir(mode=0o700)
        (folder / "archive.age").write_bytes(data)
        (folder / "generation.json").write_bytes(record.encode())
        return record

    def test_unmarked_nonempty_directory_is_not_adopted(self):
        folder = self.root / "unrelated"
        folder.mkdir(mode=0o700)
        (folder / "preserve.txt").write_text("Unrelated fixture")
        with self.assertRaises(ValueError): backup.Store(folder)
        self.assertTrue((folder / "preserve.txt").exists())

    def test_backup_data_inside_any_git_checkout_refused(self):
        checkout = self.root / "checkout"
        checkout.mkdir()
        (checkout / ".git").write_text("gitdir: fictional-worktree")
        with self.assertRaises(ValueError): backup.Store(checkout / "private-store")
        self.assertFalse((checkout / "private-store").exists())

    def test_public_store_refused(self):
        folder = self.root / "public"
        folder.mkdir(mode=0o755)
        with self.assertRaises(ValueError): backup.Store(folder)

    def test_round_trip_immutable_generation_clock(self):
        record = self.generation()
        self.assertEqual(self.store.records(), [record])
        self.assertEqual(backup.Generation.decode(record.encode()), record)

    def test_duplicate_metadata_fields_refused(self):
        record = self.generation()
        raw = record.encode().replace(b'"schema":1', b'"schema":1,"schema":1')
        with self.assertRaises(ValueError): backup.Generation.decode(raw)

    def test_all_catalogue_validates_before_any_expiry(self):
        due = self.generation(created=datetime(2026, 8, 1, tzinfo=timezone.utc))
        malformed = self.generation()
        (self.store.directory / malformed.id / "generation.json").write_text("{}")
        with self.assertRaises(ValueError): self.store.sweep(self.now)
        self.assertTrue((self.store.directory / due.id / "archive.age").exists())

    def test_changed_encrypted_bytes_refuse_expiry(self):
        record = self.generation(created=datetime(2026, 8, 1, tzinfo=timezone.utc))
        (self.store.directory / record.id / "archive.age").write_bytes(b"Fictional changed bytes")
        with self.assertRaises(ValueError): self.store.sweep(self.now)
        self.assertTrue((self.store.directory / record.id / "archive.age").exists())

    def test_symlink_archive_refused_without_changing_target(self):
        record = self.generation()
        external = self.root / "unrelated.txt"
        external.write_text("Unrelated fictional fixture")
        archive = self.store.directory / record.id / "archive.age"
        archive.unlink()
        archive.symlink_to(external)
        with self.assertRaises(ValueError): self.store.sweep(self.now)
        self.assertEqual(external.read_text(), "Unrelated fictional fixture")

    def test_tiers_expire_independently_and_idempotently(self):
        daily = self.generation()
        weekly = self.generation("weekly")
        monthly = self.generation("monthly")
        self.assertEqual(self.store.sweep(datetime(2026, 11, 28, tzinfo=timezone.utc)), 1)
        self.assertEqual({record.id for record in self.store.records()}, {daily.id, monthly.id})
        self.assertEqual(self.store.sweep(datetime(2026, 11, 29, tzinfo=timezone.utc)), 1)
        self.assertEqual(self.store.sweep(datetime(2026, 11, 29, tzinfo=timezone.utc)), 0)
        self.assertEqual(self.store.records(), [monthly])

    def test_interrupted_publication_and_expiry_recover_under_lock(self):
        for kind in ["pending", "expiring"]:
            folder = self.store.directory / ("." + kind + "-" + "a" * 32)
            folder.mkdir()
            (folder / "archive.age").write_bytes(b"Fictional unpublished/expired fixture")
        live = self.generation("monthly")
        self.assertEqual(self.store.sweep(self.now), 0)
        self.assertEqual(self.store.records(), [live])
        self.assertFalse(any(p.name.startswith((".pending-", ".expiring-")) for p in self.store.directory.iterdir()))

class BundleValidation(unittest.TestCase):
    # Synthetic archive-format fixtures only, not a database/media restore proof.
    def archive(self, folder, *, unexpected=False, duplicate=False, wrong_hash=False, link=False):
        database = b"Fictional database-format fixture"
        data = b"Fictional media-format fixture; never rendered"
        key = "photos/fictional-fixture"
        entry = "objects/" + hashlib.sha256(key.encode()).hexdigest() + ".blob"
        manifest = {"schema": 1, "role": "api", "capturedAt": "2026-10-30T00:00:00+00:00", "databaseSha256": hashlib.sha256(database).hexdigest(),
                    "objects": [{"key": key, "entry": entry, "sha256": "0" * 64 if wrong_hash else hashlib.sha256(data).hexdigest(), "bytes": len(data), "contentType": "image/jpeg"}]}
        path = folder / "bundle.tar"
        with tarfile.open(path, "w") as out:
            for name, value in [("database.dump", database), (entry, data), ("manifest.json", json.dumps(manifest).encode())]:
                info = tarfile.TarInfo(name)
                info.size = len(value)
                out.addfile(info, io.BytesIO(value))
            if unexpected:
                info = tarfile.TarInfo("../outside")
                info.size = 1
                out.addfile(info, io.BytesIO(b"x"))
            if duplicate:
                info = tarfile.TarInfo("database.dump")
                info.size = len(database)
                out.addfile(info, io.BytesIO(database))
            if link:
                info = tarfile.TarInfo("objects/link")
                info.type = tarfile.SYMTYPE
                info.linkname = "../../outside"
                out.addfile(info)
        return path

    def test_complete_bundle_validates_without_extracting_media_paths(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            manifest = backup.extract_database(self.archive(folder), folder / "database.dump")
            self.assertEqual(manifest["role"], "api")
            self.assertEqual((folder / "database.dump").read_bytes(), b"Fictional database-format fixture")
            self.assertFalse((folder / "objects").exists())

    def test_bad_members_or_changed_media_refuse_before_database_extraction(self):
        for option in ["unexpected", "duplicate", "wrong_hash", "link"]:
            with self.subTest(option=option), tempfile.TemporaryDirectory() as directory:
                folder = Path(directory)
                with self.assertRaises(ValueError): backup.extract_database(self.archive(folder, **{option: True}), folder / "database.dump")
                self.assertFalse((folder / "database.dump").exists())

if __name__ == "__main__": unittest.main()
