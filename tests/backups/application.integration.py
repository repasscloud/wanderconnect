#!/usr/bin/env python3
"""Owned actual application backup/reconciliation drill; fictional adults and synthetic non-rendered media only."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[2]
API = ROOT / "src/Swoovr.Api"
DLL = API / "bin/Debug/net10.0/Swoovr.Api.dll"
spec = importlib.util.spec_from_file_location("swoovr_backup", ROOT / "scripts/backups/backup.py")
backup = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = backup
spec.loader.exec_module(backup)


def main(on_reconciled=None):
    age = os.environ.get("SWOOVR_AGE", "age")
    keygen = str(Path(age).with_name("age-keygen"))
    source = "swoovr_backup_" + uuid.uuid4().hex
    target = "swoovr_restore_" + uuid.uuid4().hex
    created = False
    checks = 0
    def check(value, label):
        nonlocal checks
        if not value: raise AssertionError(label)
        checks += 1
    def run(args, *, env=None, cwd=None):
        reply = subprocess.run(args, env=env, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        if reply.returncode != 0:
            with tempfile.NamedTemporaryFile(prefix="swoovr-backup-drill-failure-", suffix=".log", delete=False) as diagnostic:
                diagnostic.write(reply.stderr)
                private_path = diagnostic.name
            raise RuntimeError("Owned drill tool failed: " + Path(args[0]).name + "; private diagnostics: " + private_path)
        return reply.stdout
    def sql(database, statement):
        return run(["docker", "exec", "swoovr-local-postgres-1", "psql", "-U", "swoovr", "-d", database, "-v", "ON_ERROR_STOP=1", "-tAc", statement]).strip()
    values = dict(line.split("=", 1) for line in (ROOT / ".local/docker.env").read_text().splitlines() if "=" in line and not line.startswith("#"))
    password = values["SWOOVR_DB_PASSWORD"]
    try:
        sql("postgres", "CREATE DATABASE " + source)
        created = True
        with tempfile.TemporaryDirectory(prefix="swoovr-backup-application-") as private:
            folder = Path(private)
            os.chmod(folder, 0o700)
            source_media = folder / "source-media"
            target_media = folder / "target-media"
            source_media.mkdir(mode=0o700)
            target_media.mkdir(mode=0o700)
            pg_dump = folder / "pg-dump"
            pg_dump.write_text('#!/bin/sh\nexec docker exec swoovr-local-postgres-1 pg_dump -U swoovr -d "$PGDATABASE" "$@"\n')
            pg_dump.chmod(0o700)
            producer = folder / "bundle-producer"
            producer.write_text('#!/bin/sh\ncd "$SWOOVR_BACKUP_API_DIR" || exit 1\nexec dotnet "$SWOOVR_BACKUP_API_DLL" backups bundle\n')
            producer.chmod(0o700)
            key = folder / "identity"
            run([keygen, "--output", str(key)])
            recipient = run([keygen, "-y", str(key)]).decode().strip()
            quoted_password = '"' + password.replace('"', '""') + '"'
            settings = {**os.environ, "DOTNET_ENVIRONMENT": "Development", "ASPNETCORE_ENVIRONMENT": "Development",
                        "ConnectionStrings__Swoovr": f"Host=127.0.0.1;Port=5433;Database={source};Username=swoovr;Password={quoted_password}",
                        "PGHOST": "127.0.0.1", "PGPORT": "5433", "PGDATABASE": source, "PGUSER": "swoovr", "PGPASSWORD": password, "PGSSLMODE": "prefer",
                        "Founders__Secrets__1": "Fictional-" + uuid.uuid4().hex + uuid.uuid4().hex,
                        "Beta__Enabled": "false", "Phone__Required": "false", "Staff__LocalPasswords": "true",
                        "Media__Store": "local", "Media__Local__Path": str(source_media), "Media__Screening__Worker": "false", "Retention__Worker": "false",
                        "SWOOVR_PG_DUMP": str(pg_dump), "SWOOVR_BACKUP_API_DIR": str(API), "SWOOVR_BACKUP_API_DLL": str(DLL)}
            run(["dotnet", str(DLL), "migrate"], env=settings, cwd=API)
            a, b, c, d = [uuid.uuid4().hex for _ in range(4)]
            old_media, held_media, case, report, conversation, message = [uuid.uuid4().hex for _ in range(6)]
            old_bytes = b"Fictional obsolete private media fixture; never rendered."
            held_bytes = b"Fictional held private media fixture; never rendered."
            for media_id, payload in [(old_media, old_bytes), (held_media, held_bytes)]:
                path = source_media / "photos" / media_id
                path.parent.mkdir(exist_ok=True)
                path.write_bytes(payload)
            account_values = ",".join(f"('{member}','https://issuer.example.test','{member}','Members','fictional.backup.{member}@example.test','Active',false,now(),now())" for member in [a, b, c, d])
            profile_values = ",".join(f"('{member}','Attested','1990-01-01',false,'NotStarted','NotStarted',false,false,false)" for member in [a, b, c, d])
            media_values = ",".join(f"('{media_id}','{member}','Removed',decode('{hashlib.sha256(payload).hexdigest()}','hex'),decode('{hashlib.sha256(payload).hexdigest()}','hex'),'image/jpeg',1,1,{len(payload)},now()-interval '40 months',0,false,'profile',now()-interval '39 months','photos/{media_id}')" for media_id, member, payload in [(old_media, a, old_bytes), (held_media, b, held_bytes)])
            sql(source, f"""INSERT INTO accounts(id,issuer,subject,realm,email,status,is_root_admin,created_at,last_seen_at) VALUES {account_values};
INSERT INTO member_profiles(account_id,age_status,date_of_birth,founding_member,photo_status,id_status,admin_verified,bot,employee) VALUES {profile_values};
INSERT INTO media_items(id,account_id,status,sha256,upload_sha256,content_type,width,height,bytes,created_at,screen_attempts,known_content_checked,purpose,removed_at,object_key) VALUES {media_values};
INSERT INTO moderation_cases(id,subject_account_id,status,queue,priority,created_at,updated_at,closed_at) VALUES('{case}','{b}','Closed','Support','Normal',now()-interval '40 months',now()-interval '40 months',now()-interval '40 months');
INSERT INTO reports(id,reporter_account_id,subject_account_id,subject_type,subject_ref,category_id,priority,text,case_id,created_at) VALUES('{report}','{c}','{b}','Profile','{b}','other','Normal','Fictional held review','{case}',now()-interval '40 months');
INSERT INTO case_evidence(id,case_id,report_id,kind,snapshot,captured_at,media_ids) VALUES('{uuid.uuid4().hex}','{case}','{report}','profile','{{"fictional":"held review"}}',now()-interval '40 months',ARRAY['{held_media}']::uuid[]);
INSERT INTO conversations(id,pair_low,pair_high,created_at,last_message_at) VALUES('{conversation}',least('{c}'::uuid,'{d}'::uuid),greatest('{c}'::uuid,'{d}'::uuid),now()-interval '50 days',now()-interval '40 days');
INSERT INTO messages(id,conversation_id,sender_account_id,kind,body,sent_at,deleted_at) VALUES('{message}','{conversation}','{c}','Text','Fictional expired ordinary message',now()-interval '40 days',now()-interval '31 days');
INSERT INTO push_devices(id,account_id,platform,token,created_at,last_seen_at) VALUES('{uuid.uuid4().hex}','{b}','simulated','fictional-backup-device',now(),now());""")
            store = backup.Store(folder / "store")
            old = store.create("daily", "api", recipient, dump=str(producer), age=age, env=settings, format="swoovr-bundle")
            check(old.format == "swoovr-bundle", "actual CLI produces complete bundle format")
            hold = uuid.uuid4().hex
            sql(source, f"""UPDATE accounts SET status='Deleted',email=NULL,email_accepted_at=NULL,deleted_at=now() WHERE id='{a}'; DELETE FROM member_profiles WHERE account_id='{a}'; DELETE FROM media_items WHERE id='{old_media}';
INSERT INTO legal_holds(id,status,reason,source,case_id,placed_by_role,placed_at,review_due,retention_case_ids,related_account_ids) VALUES('{hold}','Active','Fictional post-backup preservation','staff','{case}','Legal',now(),now()+interval '1 month',ARRAY['{case}']::uuid[],ARRAY['{b}']::uuid[]);
INSERT INTO legal_hold_scopes(hold_id,kind,target_id) VALUES('{hold}','Account','{b}');""")
            # Source hosts/jobs are never started in this fixture, so the post-backup authority is actually frozen.
            sql(source, backup.fence_sql(old))
            restore_env = {**settings, "PGDATABASE": "postgres", "Media__Local__Path": str(target_media)}
            backup.restore_quarantined(store, old.id, key, target, age=age, env=restore_env)
            check(sql(target, f"SELECT count(*) FROM member_profiles WHERE account_id='{a}'") == b"1", "old staged data demonstrates resurrection risk")
            check(sql(target, f"SELECT count(*) FROM legal_holds WHERE id='{hold}'") == b"0", "post-backup hold is absent before reconciliation")
            check(sql(target, "SELECT ready FROM swoovr_operations.restore_guard WHERE id=1") == b"f", "old backup never grants serving readiness")
            # Real API must exit before migrations/workers/ingress while its target is fenced.
            target_password = quoted_password
            host_env = {**restore_env, "ConnectionStrings__Swoovr": f"Host=127.0.0.1;Port=5433;Database={target};Username=swoovr;Password={target_password}"}
            with socket.socket() as probe:
                probe.bind(("127.0.0.1", 0))
                base = "http://127.0.0.1:" + str(probe.getsockname()[1])
            host_env["ASPNETCORE_URLS"] = base
            closed = subprocess.run(["dotnet", str(DLL)], env=host_env, cwd=API, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=25)
            check(closed.returncode != 0 and b"Restored database is fenced" in closed.stdout, "actual API refuses fenced startup")
            refused = False
            try: backup.replay_authority(store, old.id, key, target, age=age, env=restore_env)
            except ValueError: refused = True
            check(refused, "old backup alone cannot approve current recovery")
            authority = store.create("daily", "api", recipient, dump=str(producer), age=age, env=settings, format="swoovr-bundle", authority_fence=old.id, confirm_source_frozen=True)
            backup.complete_api_reconciliation(store, authority.id, key, target, ["dotnet", str(DLL)], age=age, env=restore_env)
            check(sql(target, f"SELECT count(*) FROM member_profiles WHERE account_id='{a}'") == b"0", "completed deletion survives old-point restore")
            check(sql(target, f"SELECT email IS NULL AND status='Deleted' FROM accounts WHERE id='{a}'") == b"t", "deleted account/contact do not resurrect")
            check(sql(target, f"SELECT count(*) FROM legal_holds WHERE id='{hold}' AND status='Active'") == b"1", "post-backup hold reconciled")
            check(sql(target, f"SELECT count(*) FROM case_evidence WHERE case_id='{case}'") == b"1", "held expired review survives actual purge")
            check(sql(target, f"SELECT body='' FROM messages WHERE id='{message}'") == b"t", "ordinary expired text purged before readiness")
            check((target_media / "photos" / held_media).read_bytes() == held_bytes, "current private held bytes restored and verified")
            check(not (target_media / "photos" / old_media).exists(), "obsolete deleted media is not imported from old archive")
            check(sql(target, "SELECT count(*) FROM push_devices") == b"0", "restored device addresses removed")
            check(sql(target, "SELECT count(*) FROM accounts WHERE sessions_revoked_at IS NULL") == b"0", "restored API sessions revoked")
            check(sql(target, "SELECT ready FROM swoovr_operations.restore_guard WHERE id=1") == b"t", "readiness recorded only after complete reconciliation")
            with tempfile.TemporaryFile() as private_log:
                host = subprocess.Popen(["dotnet", str(DLL)], env=host_env, cwd=API, stdout=private_log, stderr=private_log)
                try:
                    for attempt in range(150):
                        if host.poll() is not None: raise RuntimeError("Reconciled owned host exited unexpectedly.")
                        try:
                            if urllib.request.urlopen(base + "/health/live", timeout=1).status == 200: break
                        except (urllib.error.URLError, TimeoutError): pass
                        time.sleep(.1)
                    else: raise RuntimeError("Reconciled host readiness timeout.")
                    check(True, "actual reconciled API starts")
                    version = re.search(r'Initial\s*=\s*"([^"]+)"', (ROOT / "src/Swoovr.Contracts/ApiContract.cs").read_text())
                    request = urllib.request.Request(base + "/api/session", headers={"Swoovr-Version": version.group(1)})
                    try: urllib.request.urlopen(request)
                    except urllib.error.HTTPError as error: check(error.code == 401, "serving resumes with normal unauthenticated refusal")
                finally:
                    host.terminate()
                    try: host.wait(timeout=10)
                    except subprocess.TimeoutExpired: host.kill(); host.wait()
            if on_reconciled is not None:
                on_reconciled({"store": store, "identity": key, "age": age, "source_env": settings, "restore_env": restore_env,
                               "api_target": target, "operation": old.id, "private": folder, "pg_dump": pg_dump})
        print(f"PASS: {checks} owned application encrypted-backup/reconciliation checks: actual database/media/startup/retention; no hosted Azure, external delivery, real ownership, native UI or Keycloak restore claim.")
    finally:
        sql("postgres", "DROP DATABASE IF EXISTS " + target + " WITH (FORCE)")
        if created: sql("postgres", "DROP DATABASE " + source + " WITH (FORCE)")
        print("CLEANUP: owned source/target databases, private archives/keys and local synthetic media removed; main hosts unchanged.")


if __name__ == "__main__": main()
