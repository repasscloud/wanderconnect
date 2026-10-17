# Phase 1 — local signing-key rotation investigation

Partial Phase 1 provider evidence only. Uses the fixed local Keycloak 26.8.1 datingapp-dev realm and existing owned investigation client/fictional-adult fixture. No production OIDC adapter, real verified-email ownership, application role or privileged grant is added.

DEFAULT D23: generate a uniquely named temporary `rsa-generated` RS256/2048 provider with priority above existing key providers, retain the original provider, then delete only the component whose unique name/type/provider/realm match the current experiment. Refuse any existing investigation rotation provider rather than silently adopting/removing it. Read back the restored active key and published JWKS. No original provider is edited or removed.

The actual browser driver starts the investigation harness on 127.0.0.1:8280, signs in with the private fixture and captures responses in process memory only. It validates the original ID token; adds the temporary key; validates both original/new signatures against overlapping published keys; verifies refreshed subject/session/original auth_time; removes the temporary component; rejects its token against the newly published JWKS; validates the original token again; refreshes with the restored active key and proves logout invalid_grant. No raw token/password/identity/key material, callback URL, screenshot or browser trace is logged/exported.

Cleanup runs in finally on success and ordinary failure. SIGINT/SIGTERM close the browser to unwind pending actions and reach cleanup. A force kill, machine crash or unavailable provider can still interrupt cleanup: reruns refuse a leftover experiment prefix. Inspect only `datingapp-investigation-rotation-` key providers in the disposable realm and deliberately remove an interrupted owned component after checking ownership; never reset the volume or delete an arbitrary key provider.

Management authorization is setup, not browser-login evidence. The local helper reacquires its bootstrap-admin token before expiry and retries once after 401. This avoids a browser wait exhausting the observed 60-second management lifetime before cleanup. Credentials remain in the private ignored file; tokens remain in memory. Persistent failure still rejects rather than claiming cleanup.

## Reproduce

Use the checkout whose ignored .local/docker.env and auth-fixture.json match the retained realm volume:

```sh
./scripts/docker-local.sh up
./scripts/docker-local.sh auth-init
DATINGAPP_PLAYWRIGHT_PATH=/absolute/path/to/playwright node scripts/auth/rotation-check.mjs
DATINGAPP_PLAYWRIGHT_PATH=/absolute/path/to/playwright node scripts/auth/rotation-check.mjs --delay-cleanup
./scripts/docker-local.sh down
```

Installed Edge and Playwright are optional external tooling; normal offline checks require neither. `--delay-cleanup` holds the experiment for 65 seconds to exceed the observed management-token lifetime and asserts at least two management authorizations. Keep loopback 8280 free. When code lives in a worktree but fixtures live in the original checkout, invoke the script by absolute path with the original checkout as cwd; do not copy/print secrets or mix fixture/volume bindings.

Run `./scripts/check.sh` before commits. Synthetic helper tests cover successful cleanup, exercise failure, existing-prefix refusal, lost create response and expired management authorization. They do not count as actual provider rotation; recorded Edge/provider results do.

Sources: [Keycloak key rotation](https://www.keycloak.org/docs/latest/server_admin/index.html#rotating-keys), [pinned Admin REST component operations](https://www.keycloak.org/docs-api/26.8.1/rest-api/index.html#_components). Observations are specific to the local pin/configuration, not a production rotation/revocation policy or key-cache acceptance test for a future API adapter. Real social/linking, mail/ownership, MFA/recovery, role persistence, product freshness and mobile/device gates remain open.
