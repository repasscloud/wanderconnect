#!/usr/bin/env python3
"""Paired owned API/Keycloak restore drill, invoked only by the owned provider component runner."""
import importlib.util
import http.client
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("application_backup_drill", Path(__file__).with_name("application.integration.py"))
application = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = application
spec.loader.exec_module(application)
backup = application.backup


def main():
    container = os.environ.get("SWOOVR_TEST_BACKUP_KC_CONTAINER", "")
    database = os.environ.get("SWOOVR_TEST_BACKUP_KC_DATABASE", "")
    base = os.environ.get("SWOOVR_TEST_BACKUP_KC_BASE", "")
    if not re.fullmatch(r"swoovr-kc-retire-[0-9a-f]{32}", container) or database != container.replace("-", "_") or not re.fullmatch(r"http://127\.0\.0\.1:[0-9]+", base):
        raise ValueError("A matching owned provider/database/loopback context is required.")
    def run(args, *, env=None):
        result = subprocess.run(args, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        if result.returncode != 0: raise RuntimeError("Owned paired restore tool failed; no private diagnostics echoed.")
        return result.stdout.strip()
    if run(["docker", "port", container, "8080/tcp"]).decode() != base.removeprefix("http://"):
        raise ValueError("Provider URL does not match the owned Docker mapping.")
    inspection = json.loads(run(["docker", "inspect", container]))[0]
    source_env = dict(value.split("=", 1) for value in inspection["Config"]["Env"] if "=" in value)
    if not source_env["KC_DB_URL"].endswith("/" + database): raise ValueError("Owned source database mismatch.")
    realm = "swoovr-contact-retirement-test"
    def request(route, method="GET", body=None, token=None, form=False):
        data = urllib.parse.urlencode(body).encode() if form else json.dumps(body).encode() if body is not None else None
        headers = {"Content-Type": "application/x-www-form-urlencoded" if form else "application/json"}
        if token: headers["Authorization"] = "Bearer " + token
        call = urllib.request.Request(base + route, data=data, method=method, headers=headers)
        try: response = urllib.request.urlopen(call, timeout=10)
        except urllib.error.HTTPError as error: response = error
        raw = response.read()
        try: value = json.loads(raw) if raw else None
        except json.JSONDecodeError: value = None
        return response.status, value
    def master():
        status, reply = request("/realms/master/protocol/openid-connect/token", "POST", {"grant_type": "password", "client_id": "admin-cli", "username": source_env["KC_BOOTSTRAP_ADMIN_USERNAME"], "password": source_env["KC_BOOTSTRAP_ADMIN_PASSWORD"]}, form=True)
        if status != 200: raise RuntimeError("Owned bootstrap authentication failed.")
        return reply["access_token"]
    token = master()
    identities = []
    for label in ["deleted", "surviving"]:
        email = "fictional.backup." + label + "." + uuid.uuid4().hex + "@example.test"
        password = "Fictional-" + uuid.uuid4().hex
        status, _ = request("/admin/realms/" + realm + "/users", "POST", {"username": email, "email": email, "firstName": "Fictional", "lastName": "Adult", "enabled": True, "emailVerified": True, "credentials": [{"type": "password", "value": password, "temporary": False}]}, token=token)
        if status != 201: raise RuntimeError("Owned fictional provider fixture failed.")
        _, users = request("/admin/realms/" + realm + "/users?exact=true&email=" + urllib.parse.quote(email), token=token)
        identities.append({"id": users[0]["id"], "email": email, "password": password})
    def paired(context):
        store, age, identity = context["store"], context["age"], context["identity"]
        settings = {**context["source_env"], "PGDATABASE": database}
        restore_env = context["restore_env"]
        target = "swoovr_restore_" + uuid.uuid4().hex
        restored_container = "swoovr-kc-backup-" + uuid.uuid4().hex
        started = False
        checks = 0
        def check(value, label):
            nonlocal checks
            if not value: raise AssertionError(label)
            checks += 1
        def sql(db, statement):
            return run(["docker", "exec", "swoovr-local-postgres-1", "psql", "-U", "swoovr", "-d", db, "-v", "ON_ERROR_STOP=1", "-tAc", statement])
        try:
            public = run([str(Path(age).with_name("age-keygen")), "-y", str(identity)]).decode()
            old = store.create("daily", "keycloak", public, dump=str(context["pg_dump"]), age=age, env=settings)
            deleted, surviving = identities
            check(request("/admin/realms/" + realm + "/users/" + deleted["id"], "DELETE", token=master())[0] == 204, "provider deletion occurs after old snapshot")
            run(["docker", "stop", container])
            # No process now writes this owned identity database; current authority is actually frozen.
            sql(database, backup.fence_sql(old, context["operation"]))
            backup.restore_quarantined(store, old.id, identity, target, age=age, env=restore_env, operation_id=context["operation"])
            check(sql(target, "SELECT count(*) FROM user_entity WHERE id='" + deleted["id"] + "'") == b"1", "old identity snapshot demonstrates deleted-credential resurrection risk")
            check(sql(target, "SELECT ready FROM swoovr_operations.restore_guard WHERE id=1") == b"f", "identity target remains quarantined")
            authority = store.create("daily", "keycloak", public, dump=str(context["pg_dump"]), age=age, env=settings, authority_fence=context["operation"], confirm_source_frozen=True)
            refused = False
            try: backup.finalize_keycloak(store, authority.id, target, context["api_target"], env=restore_env)
            except ValueError: refused = True
            check(refused, "identity readiness cannot open before current identity authority is replayed")
            backup.replay_authority(store, authority.id, identity, target, age=age, env=restore_env)
            check(sql(target, "SELECT count(*) FROM user_entity WHERE id='" + deleted["id"] + "'") == b"0", "current authority removes deleted identity/credential rows")
            check(sql(target, "SELECT count(*) FROM credential WHERE user_id='" + surviving["id"] + "' AND type='password'") == b"1", "current surviving credential retained")
            backup.finalize_keycloak(store, authority.id, target, context["api_target"], env=restore_env)
            check(sql(target, "SELECT ready FROM swoovr_operations.restore_guard WHERE id=1") == b"t", "identity readiness requires same-operation completed API receipt")
            network = next(iter(inspection["NetworkSettings"]["Networks"]))
            jar = next(mount["Source"] for mount in inspection["Mounts"] if mount["Destination"] == "/opt/keycloak/providers/swoovr-contact-policy.jar")
            restored_env = {**os.environ, **{key: source_env[key] for key in ["KC_DB", "KC_DB_USERNAME", "KC_DB_PASSWORD", "KC_BOOTSTRAP_ADMIN_USERNAME", "KC_BOOTSTRAP_ADMIN_PASSWORD", "KC_HOSTNAME_STRICT"]}, "KC_DB_URL": "jdbc:postgresql://swoovr-local-postgres-1:5432/" + target}
            names = ["KC_DB", "KC_DB_URL", "KC_DB_USERNAME", "KC_DB_PASSWORD", "KC_BOOTSTRAP_ADMIN_USERNAME", "KC_BOOTSTRAP_ADMIN_PASSWORD", "KC_HOSTNAME_STRICT"]
            args = ["docker", "run", "-d", "--name", restored_container, "--network", network, "--cpus", "2", "--memory", "1536m", "-p", base.removeprefix("http://") + ":8080", "--mount", "type=bind,src=" + jar + ",dst=/opt/keycloak/providers/swoovr-contact-policy.jar,readonly"]
            for name in names: args += ["-e", name]
            run([*args, inspection["Image"], "start-dev"], env=restored_env)
            started = True
            for attempt in range(150):
                try:
                    if request("/realms/master/.well-known/openid-configuration")[0] == 200: break
                except (urllib.error.URLError, TimeoutError, http.client.RemoteDisconnected, ConnectionResetError): pass
                time.sleep(1)
            else: raise RuntimeError("Restored owned provider readiness timed out.")
            check(request("/admin/serverinfo", token=master())[1]["systemInfo"]["version"] == "26.8.1", "actual pinned provider starts on restored database")
            def login(person):
                return request("/realms/" + realm + "/protocol/openid-connect/token", "POST", {"grant_type": "password", "client_id": "swoovr-contact-policy-fixture", "username": person["email"], "password": person["password"]}, form=True)
            check(login(surviving)[0] == 200, "real provider authenticates surviving fictional credential after restoration")
            rejected_status, rejected = login(deleted)
            # Pinned26.8.1 ValidateUsername returns400 invalid_grant for an unknown user (not401).
            check(rejected_status == 400 and isinstance(rejected, dict) and rejected.get("error") == "invalid_grant" and "access_token" not in rejected,
                  "real provider refuses deleted credential: status=" + str(rejected_status) + ", error=" + str(rejected.get("error") if isinstance(rejected, dict) else None))
            check(sql(context["api_target"], "SELECT ready FROM swoovr_operations.restore_guard WHERE id=1") == b"t", "paired API receipt remains complete")
            print(f"PASS: {checks} paired owned Keycloak restore checks with actual26.8.1 provider; direct grant is a disposable protocol fixture, not a product client or real ownership/delivery/hosted Entra proof.")
        finally:
            if started: run(["docker", "rm", "-f", restored_container])
            sql("postgres", "DROP DATABASE IF EXISTS " + target + " WITH (FORCE)")
            print("CLEANUP: owned restored provider/database removed; source provider remains stopped for its parent cleanup.")
    application.main(on_reconciled=paired)


if __name__ == "__main__": main()
