#!/usr/bin/env python3
"""One scheduled publisher execution; Azure hosting supplies the daily trigger and private PG*/media/Key Vault configuration."""
import argparse
from datetime import datetime
import os
from pathlib import Path
import sys
import tempfile

from azure_store import connect, core


def due_tiers(records, role, now):
    core.utc(now)
    def same_period(record, tier):
        if record.role != role or record.tier != tier: return False
        if tier == "daily": return record.created.date() == now.date()
        if tier == "weekly": return record.created.isocalendar()[:2] == now.isocalendar()[:2]
        return (record.created.year, record.created.month) == (now.year, now.month)
    return [tier for tier in ("daily", "weekly", "monthly") if not any(same_period(record, tier) for record in records)]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--account-url", required=True)
    parser.add_argument("--container", required=True)
    parser.add_argument("--client-id", required=True)
    parser.add_argument("--role", choices=("api", "keycloak"), required=True)
    parser.add_argument("--recipient", required=True)
    parser.add_argument("--bundle-producer")
    args = parser.parse_args()
    if args.role == "api" and not args.bundle_producer: raise ValueError("API generations require referenced media, not a database-only dump.")
    if args.role == "keycloak" and args.bundle_producer: raise ValueError("Identity generations use the PostgreSQL archive format.")
    with connect(args.account_url, args.container, args.client_id) as remote, tempfile.TemporaryDirectory(prefix="swoovr-publish-private-") as temporary:
        os.chmod(temporary, 0o700)
        local = core.Store(Path(temporary) / "encrypted-generations")
        # Single-execution scheduler parallelism must be1 per role. Retries inspect remote commit markers first.
        missing = due_tiers(remote.catalogue(), args.role, datetime.now(core.UTC))
        for tier in missing:
            record = local.create(tier, args.role, args.recipient, dump=args.bundle_producer or os.environ.get("SWOOVR_PG_DUMP", "pg_dump"),
                                  age=os.environ.get("SWOOVR_AGE", "age"), format="swoovr-bundle" if args.bundle_producer else "postgres-custom")
            remote.publish(local, record.id)
        print(f"Scheduled encrypted generations published: {len(missing)}. Existing UTC periods preserved.")


if __name__ == "__main__":
    try: main()
    except Exception:
        print("Scheduled backup failed; uncommitted/remaining generations require retry. No success or simulation fallback.", file=sys.stderr)
        raise SystemExit(1)
