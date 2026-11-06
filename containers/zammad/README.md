# Local Zammad source and boundary

`compose.upstream.yaml` is vendored without edits from upstream tag **v17.5.2**, commit **b51cba1**, <https://github.com/zammad/zammad-docker-compose/tree/v17.5.2>. Its AGPL-3.0 license is retained in `LICENSE`. DEFAULT pin: Zammad image `ghcr.io/zammad/zammad:7.2.2-0000`; running application reports `7.2.2-295796a2.docker`. The local override also pins every pulled image by its verified registry digest. Supporting release versions remain those of the upstream file. Do not float images during acceptance.

`compose.local.yaml` binds nginx only to `127.0.0.1:8095` (screening uses 8080). The provider database, Redis, memcache, Elasticsearch and Rails ports remain unpublished. Rails and scheduler reach the Swoovr shared network anchor as `swoovr-platform:5080`; the API reaches nginx as `swoovr-zammad:8080`. Each Compose project owns separate volumes. The private integration network is named `swoovr-ticketing`.

Official installation guidance: <https://docs.zammad.org/en/latest/install/docker-compose.html>. At least 4 GB RAM is required for Zammad; this local Docker engine has roughly 8 GB total shared with Swoovr. Observe resource use before accepting full concurrent-load results.

```sh
python3 scripts/zammad_local.py init
python3 scripts/zammad_local.py up
python3 scripts/zammad_local.py status
# Once Rails is healthy:
python3 scripts/zammad_local.py provision
# Repeat provisioning to confirm idempotence.
python3 scripts/zammad_local.py down
# DESTRUCTIVE, explicit opt-in; only when local dataset is disposable:
python3 scripts/zammad_local.py reset --delete-local-data
```

Generated secrets and captured token results live in ignored `.local/zammad/` with mode 0600. Never paste that directory into evidence or commits. The provisioner uses the documented Rails runner bootstrap mechanism; Ruby models are a version-pinned bootstrap compatibility dependency, not public runtime APIs. Runtime code must use documented HTTP APIs. Outgoing notification mail is routed only to the existing local Mailpit sink; no external delivery is configured. The webhook/trigger stay inactive unless `provision --events` is used. Provisioner, identity-lifecycle and staff-directory tokens are distinct; none is an application-wide agent token. Staff Zammad identities and their tokens are created by the Swoovr API's staff synchronisation from Swoovr roles (D327), not by an operator script. Staff sign in to Zammad's web interface only through the staff Keycloak realm; locally, `local-keycloak-forward.rb` (pinned Zammad image) lets the Rails container reach `localhost:8180`, using a throwaway certificate kept in `.local/zammad/tls`. `Users` is disabled only when empty, preserving historical tickets otherwise.

Start the existing platform first. Re-run `up` after its Mailpit anchor is recreated to restore the private network connection. No changes to OIDC URLs or Swoovr's existing launcher are required.

This stack is local infrastructure. Remote hosting, TLS and mailbox/DNS setup remain external acceptance work. Licensing is decided (D333, #207): keep this unmodified upstream Compose file and `LICENSE`; reassess before modifying Zammad itself. The separately deployed public Cloudflare website is outside this implementation.

Local provider/security acceptance (after provisioning):

```sh
python3 tests/zammad/provider_acceptance.py
dotnet run --project tests/Swoovr.Checks -- --zammad-live .local/zammad
python3 tests/zammad/webhook_acceptance.py
python3 tests/zammad/backup_acceptance.py
```

The webhook harness owns and removes a temporary API/database, temporarily changing the local webhook/trigger before restoring them inactive. Run it on this disposable local instance only. The database backup check restores into a new database and reports its filesystem/full-recovery gap explicitly. Full repository checks include lifecycle, portable provider/security checks and PostgreSQL state/uncertainty/concurrency cases when `SWOOVR_TEST_DATABASE` points to the running local database.
