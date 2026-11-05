#!/usr/bin/env python3
"""Full local Zammad recovery into an isolated clone (never the source). The pinned 7.2.2 dataset stores attachments in
PostgreSQL (storage_provider=DB), so the database dump carries files too. The clone runs from the same pinned image digests
on an *internal* Docker network (no route out), so restored webhooks, triggers or mail cannot reach Swoovr or anyone.
It is verified over HTTP with the restored credentials: ticket, articles and an attachment's exact bytes. Prints counts
and pass/fail only; the dump and passwords stay private and the clone is removed afterwards.
"""
import hashlib, json, os, secrets, subprocess, time, uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PRIVATE = ROOT / '.local/zammad'
SOURCE_DB = 'swoovr-zammad-zammad-postgresql-1'
SOURCE_RAILS = 'swoovr-zammad-zammad-railsserver-1'


def run(*args, **kw):
    return subprocess.run(list(args), check=True, capture_output=True, text=kw.pop('text', True), **kw)


def image(service):
    # The digest the running source uses, so the clone is the same pinned release.
    return json.loads(run('docker', 'inspect', service).stdout)[0]['Config']['Image']


def main():
    tag = uuid.uuid4().hex[:8]
    net, db, redis, cache, rails = (f'swoovr-zammad-restore-{tag}{s}' for s in ('', '-db', '-redis', '-cache', '-rails'))
    password = secrets.token_urlsafe(24)
    token = json.loads((PRIVATE / 'provisioned.json').read_text())['provisioning_token']
    checks = []
    def check(ok, label):
        if not ok: raise RuntimeError('FAIL: ' + label)
        checks.append(label); print('PASS:', label, flush=True)
    dump = PRIVATE / f'restore-{tag}.dump'
    fd = os.open(dump, os.O_CREAT | os.O_WRONLY | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    try:
        storage = run('docker', 'exec', SOURCE_RAILS, 'bundle', 'exec', 'rails', 'runner', 'puts Setting.get("storage_provider")').stdout.strip().splitlines()[-1]
        check(storage == 'DB', 'pinned dataset keeps attachment content in PostgreSQL, so the database backup includes files')
        with os.fdopen(fd, 'wb') as stream:
            subprocess.run(['docker', 'exec', SOURCE_DB, 'pg_dump', '-U', 'postgres', '-Fc', 'zammad_production'], stdout=stream, check=True)
        counts = "SELECT (SELECT count(*) FROM tickets)||'|'||(SELECT count(*) FROM ticket_articles)||'|'||(SELECT count(*) FROM users)||'|'||(SELECT count(*) FROM stores)||'|'||(SELECT count(*) FROM store_provider_dbs)"
        before = run('docker', 'exec', SOURCE_DB, 'psql', '-U', 'postgres', '-d', 'zammad_production', '-At', '-c', counts).stdout.strip()
        sample = run('docker', 'exec', SOURCE_DB, 'psql', '-U', 'postgres', '-d', 'zammad_production', '-At', '-c',
                     "SELECT s.o_id||'|'||s.id FROM stores s JOIN store_objects o ON o.id=s.store_object_id WHERE o.name='Ticket::Article' ORDER BY s.id DESC LIMIT 1").stdout.strip()
        article_id, store_id = (int(x) for x in sample.split('|'))
        ticket_id = int(run('docker', 'exec', SOURCE_DB, 'psql', '-U', 'postgres', '-d', 'zammad_production', '-At', '-c', f'SELECT ticket_id FROM ticket_articles WHERE id={article_id}').stdout.strip())
        source_bytes = subprocess.run(['docker', 'exec', SOURCE_RAILS, 'curl', '-sf', '-H', 'Authorization: Token token=' + token,
                                       f'http://localhost:3000/api/v1/ticket_attachment/{ticket_id}/{article_id}/{store_id}'], capture_output=True, check=True).stdout
        source_title = json.loads(subprocess.run(['docker', 'exec', SOURCE_RAILS, 'curl', '-sf', '-H', 'Authorization: Token token=' + token,
                                                  f'http://localhost:3000/api/v1/tickets/{ticket_id}'], capture_output=True, check=True).stdout)['title']

        run('docker', 'network', 'create', '--internal', net)
        run('docker', 'run', '-d', '--name', db, '--network', net, '--network-alias', 'zammad-postgresql', '-e', 'POSTGRES_USER=zammad',
            '-e', 'POSTGRES_PASSWORD=' + password, '-e', 'POSTGRES_DB=zammad_production', image(SOURCE_DB))
        for _ in range(60):
            if subprocess.run(['docker', 'exec', db, 'pg_isready', '-U', 'zammad', '-d', 'zammad_production'], capture_output=True).returncode == 0: break
            time.sleep(1)
        with dump.open('rb') as stream:
            subprocess.run(['docker', 'exec', '-i', db, 'pg_restore', '-U', 'zammad', '--no-owner', '--role=zammad', '--exit-on-error', '-d', 'zammad_production'], stdin=stream, check=True, capture_output=True)
        after = run('docker', 'exec', db, 'psql', '-U', 'zammad', '-d', 'zammad_production', '-At', '-c', counts).stdout.strip()
        check(after == before, 'restored clone has identical ticket, article, user, attachment and file-content counts')
        run('docker', 'run', '-d', '--name', redis, '--network', net, '--network-alias', 'zammad-redis', image('swoovr-zammad-zammad-redis-1'))
        run('docker', 'run', '-d', '--name', cache, '--network', net, '--network-alias', 'zammad-memcached', image('swoovr-zammad-zammad-memcached-1'), 'memcached', '-m', '64M')
        run('docker', 'run', '-d', '--name', rails, '--network', net, '-e', 'POSTGRESQL_HOST=zammad-postgresql', '-e', 'POSTGRESQL_USER=zammad',
            '-e', 'POSTGRESQL_PASS=' + password, '-e', 'POSTGRESQL_DB=zammad_production', '-e', 'POSTGRESQL_DB_CREATE=false',
            '-e', 'REDIS_URL=redis://zammad-redis:6379', '-e', 'MEMCACHE_SERVERS=zammad-memcached:11211', '-e', 'ELASTICSEARCH_ENABLED=false',
            image(SOURCE_RAILS), 'zammad-railsserver')
        def fetch(path, binary=False):
            done = subprocess.run(['docker', 'exec', rails, 'curl', '-sf', '-H', 'Authorization: Token token=' + token, 'http://localhost:3000/api/v1/' + path], capture_output=True)
            return None if done.returncode else (done.stdout if binary else json.loads(done.stdout))
        for _ in range(180):
            if subprocess.run(['docker', 'exec', rails, 'curl', '-sf', 'http://localhost:3000/'], capture_output=True).returncode == 0: break
            time.sleep(2)
        else:
            logs = subprocess.run(['docker', 'logs', '--tail', '40', rails], capture_output=True, text=True)
            lines = [l[:200] for l in (logs.stdout + logs.stderr).splitlines() if any(k in l for k in ('Error', 'error', 'FATAL', 'refused', 'waiting'))]
            print('\n'.join(lines[-8:]))
            raise RuntimeError('FAIL: restored Rails did not start')
        me = fetch('users/me')
        check(me is not None and me.get('login'), 'restored clone answers its API with the restored provisioning credential')
        restored = fetch(f'tickets/{ticket_id}')
        articles = fetch(f'ticket_articles/by_ticket/{ticket_id}')
        check(restored is not None and restored['title'] == source_title and any(a['id'] == article_id for a in articles), 'restored ticket and its conversation are readable')
        data = fetch(f'ticket_attachment/{ticket_id}/{article_id}/{store_id}', binary=True)
        check(data is not None and hashlib.sha256(data).digest() == hashlib.sha256(source_bytes).digest(), 'restored attachment bytes are identical to the source')
        egress = subprocess.run(['docker', 'exec', rails, 'curl', '-s', '--max-time', '5', '-o', '/dev/null', 'http://swoovr-platform:5080/health/live'], capture_output=True).returncode
        check(egress != 0, 'the clone has no route to Swoovr, so restored webhooks and mail cannot leave it')
        print(f'PASS: {len(checks)} full-recovery checks (search index not rebuilt: the clone runs without Elasticsearch; see operator notes).')
    finally:
        for name in (rails, cache, redis, db):
            subprocess.run(['docker', 'rm', '-f', '-v', name], capture_output=True)
        subprocess.run(['docker', 'network', 'rm', net], capture_output=True)
        try: dump.unlink()
        except FileNotFoundError: pass


if __name__ == '__main__':
    main()
