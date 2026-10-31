# UAT one-shot operator tools

D284 DEFAULT under D221/D283. This image is a local preparation artifact, not a deployed scheduler or successful Azure backup. Build from the repository root:

```
docker build --platform linux/amd64 -f containers/uat/Dockerfile --target operator -t swoovr-uat-operator:phase-zero-check .
python3 tests/uat/operator_image.py
python3 tests/uat/database_bootstrap.py
```

The existing API publish is combined with PostgreSQL17.11 clients, age and Python/Azure managed-identity backup dependencies. The official PGDG signing key is SHA256-checked and its repository signature verifies packages; client package revision17.11-1.pgdg24.04+2 is explicit. Ubuntu dependency/security packages are resolved at build time; record the resulting image digest and installed-package inventory for the approved deployment. Never deploy a mutable local tag. Runtime is the existing unprivileged UID1654. No new .NET project, server or credentials are baked in.

Entry points: `tools-check`, `database-bootstrap`, `backup api`, `backup keycloak`, `backup-expiry`, `recovery-help`. Unknown/incomplete commands fail with fixed diagnostics. `recovery-help` performs no restoration: the existing D261 paired/fenced restore, current-authority reconciliation and ingress approval remain mandatory. The Keycloak bootstrap job remains subsequent software work; the dispatcher currently refuses that command.

`database-bootstrap` reads only runtime PG*/role-password bindings. Outside Development it requires verify-full and an existing trust bundle. It uses psql stdin, suppresses private SQL diagnostics and sends SCRAM verifiers rather than raw passwords in SQL. Passwords require32–1024 printable ASCII characters without spaces. The administrator needs CREATEROLE, database ownership and the ordinary PostgreSQL role administration/membership rights; it must not be assumed to be a superuser. Existing privileged application-role names are refused rather than silently demoted. Bootstrap is explicit and repeatable; it updates the selected credentials and ownership/grants, so rotate references/connections in a coordinated maintenance window.

The migrator owns the application database and restricted schema. API/operator logins have data permissions, not schema/role creation. The Keycloak login owns only its database. The backup login has read access/default privileges across both databases and cannot write. Default privileges cover later migration/Keycloak tables; local acceptance exercises a full real API migration under the separate owner. Azure-managed administrator/TLS acceptance remains an operator gate; the local fixture deliberately uses a non-superuser administrator and local transport, not an Azure simulation claim.

Scheduled dispatch invokes the existing `scheduled.py`/`azure_store.py` with explicit account, container, managed-identity client ID, role and public age recipient. API capture invokes the published API snapshot/media bundle producer; identity capture uses pg_dump. No age private identity goes to a publisher. Encrypted publication/commit markers, exact retention and expiry/recovery separation remain D261. Each job is single-completion with no automatic replica retry. Before enabling schedules, validate real managed-identity Blob permissions, non-overlap, populated referenced-media capture, alerts and paired restore. API empty-object bundle and real age roundtrip/tamper checks do not establish those hosted guarantees.

Sources: [official PostgreSQL Ubuntu/PGDG installation](https://www.postgresql.org/download/linux/ubuntu/), [existing encrypted backup runbook](BACKUP-RESTORE.md). The final deployment runbook will provide the staged job/operator commands; these instructions authorise no provisioning or execution against UAT.
