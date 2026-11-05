#!/usr/bin/env python3
"""Exercise isolated local Zammad database backup/restore without overwriting the source dataset."""
import os
from pathlib import Path
import subprocess
import uuid
ROOT = Path(__file__).resolve().parents[2]
PRIVATE = ROOT / '.local/zammad'

def main():
    database = 'zammad_restore_' + uuid.uuid4().hex[:12]
    container = 'swoovr-zammad-zammad-postgresql-1'
    def sql(db, statement):
        return subprocess.run(['docker', 'exec', container, 'psql', '-U', 'postgres', '-d', db, '-At', '-v', 'ON_ERROR_STOP=1', '-c', statement],
            check=True, capture_output=True, text=True).stdout.strip()
    backup = PRIVATE / 'acceptance-database.dump'
    descriptor = os.open(backup, os.O_CREAT | os.O_WRONLY | os.O_NOFOLLOW, 0o600)
    os.fchmod(descriptor, 0o600)
    os.ftruncate(descriptor, 0)
    with os.fdopen(descriptor, 'wb') as stream:
        subprocess.run(['docker', 'exec', container, 'pg_dump', '-U', 'postgres', '-Fc', 'zammad_production'], stdout=stream, check=True)
    before = sql('zammad_production', 'SELECT (SELECT count(*) FROM tickets),(SELECT count(*) FROM ticket_articles),(SELECT count(*) FROM users),(SELECT count(*) FROM stores)')
    sql('postgres', 'CREATE DATABASE ' + database)
    try:
        with backup.open('rb') as stream:
            subprocess.run(['docker', 'exec', '-i', container, 'pg_restore', '-U', 'postgres', '--exit-on-error', '-d', database], stdin=stream, check=True, capture_output=True)
        after = sql(database, 'SELECT (SELECT count(*) FROM tickets),(SELECT count(*) FROM ticket_articles),(SELECT count(*) FROM users),(SELECT count(*) FROM stores)')
        assert before == after, 'restored data counts differ'
        print('PASS: Zammad custom-format PostgreSQL backup restored into disposable database; ticket/article/customer/attachment-store counts match.')
        print('GAP: filesystem storage and full application disaster-recovery restoration are not established by this database check.')
    finally:
        sql('postgres', 'DROP DATABASE ' + database + ' WITH (FORCE)')

if __name__ == '__main__': main()
