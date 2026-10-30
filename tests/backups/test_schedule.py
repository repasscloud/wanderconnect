"""Pure UTC cadence checks; no scheduled host, Azure authentication or storage evidence."""
from datetime import datetime, timezone
import importlib.util
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts/backups"))
SPEC = importlib.util.spec_from_file_location("swoovr_backup_schedule", ROOT / "scripts/backups/scheduled.py")
schedule = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = schedule
SPEC.loader.exec_module(schedule)


def record(tier, when, role="api"):
    return schedule.core.Generation("a" * 32, tier, role, when, schedule.core.expires_at(tier, when), "b" * 64)


class Cadence(unittest.TestCase):
    def test_first_success_seeds_all_periods_even_after_month_start(self):
        self.assertEqual(schedule.due_tiers([], "api", datetime(2026, 10, 30, tzinfo=timezone.utc)), ["daily", "weekly", "monthly"])

    def test_successful_retry_does_not_duplicate_current_periods(self):
        now = datetime(2026, 10, 30, tzinfo=timezone.utc)
        self.assertEqual(schedule.due_tiers([record(tier, now) for tier in ["daily", "weekly", "monthly"]], "api", now), [])

    def test_daily_moves_without_refreshing_weekly_monthly_clocks(self):
        before = datetime(2026, 10, 29, tzinfo=timezone.utc)
        self.assertEqual(schedule.due_tiers([record(tier, before) for tier in ["daily", "weekly", "monthly"]], "api", datetime(2026, 10, 30, tzinfo=timezone.utc)), ["daily"])

    def test_iso_week_boundary_handles_new_year(self):
        before = datetime(2026, 12, 31, tzinfo=timezone.utc)
        self.assertEqual(schedule.due_tiers([record(tier, before) for tier in ["daily", "weekly", "monthly"]], "api", datetime(2027, 1, 1, tzinfo=timezone.utc)), ["daily", "monthly"])

    def test_other_database_role_never_satisfies_a_generation(self):
        now = datetime(2026, 10, 30, tzinfo=timezone.utc)
        self.assertEqual(schedule.due_tiers([record(tier, now, "keycloak") for tier in ["daily", "weekly", "monthly"]], "api", now), ["daily", "weekly", "monthly"])

if __name__ == "__main__": unittest.main()
