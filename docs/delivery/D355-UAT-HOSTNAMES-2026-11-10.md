# D355 UAT hostnames and deployment readiness

[Owner instruction #124](https://github.com/repasscloud/wanderconnect/issues/124#issuecomment-6742633182), 10 November 2026. **CONFIRMED:** single-level hostnames and Universal SSL coverage replace D291's nested names and paid edge certificate requirement. Production remains Azure. **OPEN:** UAT hosting, including Vultr. Existing Azure infrastructure is an optional path. No resource has been deployed, merged or provisioned by this change.

| Component | Confirmed UAT host |
| --- | --- |
| Member Web / passkey RP | `uat.swoovr.app` |
| API | `api-uat.swoovr.app` |
| Admin | `admin-uat.swoovr.app` |
| Keycloak | `auth-uat.swoovr.app` |
| Future venue portal | `venues-uat.swoovr.app` |
| Helpdesk (Zammad) | `helpdesk-uat.swoovr.app` (owner addition in chat, 10 November 2026; [Q176](QUESTIONS.md#q176--uat-helpdesk-hostname-and-zammad-outgoing-mail)) |
| Upload Worker | `images-upload-uat.swoovr.app` |
| Delivery Worker | `images-uat.swoovr.app` |

## Configuration and authentication audit

Active Bicep app hosts, API issuer/client settings, mobile UAT endpoint defaults, Keycloak generator/tests, hosting proxy/CSP checks, deployment guides and Worker handoff use these names. The member origin and mobile `com.swoovr.app:/oauth2redirect` scheme do not change. Confidential portal callbacks/logout URLs stay exact and generated per origin. The local-only support client is excluded from hosted realm exports. Realms remain disabled until operator acceptance; the venue hostname reserves future use only.

The member passkey RP remains `uat.swoovr.app`. Since the auth host is now a sibling, the Web app supplies `GET /.well-known/webauthn` only when its configured public origin is exactly `https://uat.swoovr.app` and its configured authority is exactly `https://auth-uat.swoovr.app/realms/swoovr-members` (optional trailing slash). The response authorises only `https://auth-uat.swoovr.app`; other host/configuration combinations return 404 with an empty list. It never reflects request Origin or grants production origins. Browser related-origin support and the pinned Keycloak ceremony require hosted acceptance before passkeys are considered ready.

BFF API/issuer trust continues to derive from explicit configuration. Cookies retain host-only defaults; no parent cookie domain or cross-origin browser API trust is added. CSP form-action derives from the configured authority. Configure `Hosting__PublicOrigin`, `AllowedHosts`, `Keycloak__Authority`, `Api__BaseUrl` and exact `Hosting__TrustedProxyNetworks` for each selected application host. Those variables work independently of provider; never trust arbitrary forwarded headers or broad proxy ranges. Use realm-specific authorities for member and staff. On a non-Azure host provide ordinary runtime secrets privately; Azure Key Vault references are an optional implementation.

Universal SSL covers the single-level public names in a full Cloudflare zone; verify actual issuance before traffic. Origin TLS still requires a valid certificate on the selected origin. [EDGE-UAT.md](EDGE-UAT.md) separates portable TLS/DNS requirements from optional Azure certificate bindings and `asuid` ownership TXT records. Worker custom domains need no Azure TXT records.

Historical decisions and dated records retain their text with pointers. [KEYCLOAK-UAT.md](KEYCLOAK-UAT.md), [NATIVE-UAT.md](NATIVE-UAT.md), [DEPLOYMENT-UAT.md](DEPLOYMENT-UAT.md) and the Worker handoff describe the current configuration. Separate #261 work supplies API staging settings/migration; this change does not enable delivery.

## Validation evidence and deployment checks

Local evidence on 10 November 2026:

- `env -u SWOOVR_TEST_DATABASE ./scripts/check.sh` — exit 0; Release build: zero warnings/errors; Node suites: 109 + 36 passed (including four related-origin HTTP cases); all three local HTTP smoke stages passed. Persistence checks explicitly skipped (no test PostgreSQL configuration).
- `PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tests/uat -p 'test_*.py'` — 9 passed, including exact callback/logout, RP, Entra isolation and invalid-origin rejection. The initial new-host tests failed against the previous generator; both the hostname rules and local-only client leak were corrected before the passing run.
- `python3 tests/uat/infrastructure.py --bicep /tmp/swoovr-bicep-d355/bicep-osx-arm64` — 74 compiled infrastructure/security assertions passed with Bicep 0.49.2, no Azure access.
- Related-origin HTTP test was observed failing before implementation; current tests exercise actual local Web hosts, exact positive UAT JSON, unrelated/production authorities and production hosts returning an empty 404, no reflected Origin and no-store caching.
- Repository search leaves nested UAT hostnames only in historical records and an intentional wildcard negative test. GitHub Actions remain unchanged/commented.

Full local logs are in ignored `.local/d355/`; this committed record is the nonsecret evidence summary. No live DNS, certificate issuance, provider tenant, native device or hosted passkey ceremony has been tested. Before admitting users, verify exact OIDC login/logout, issuer validation, host/proxy controls, cookie scope, related-origin JSON without redirects/challenges and passkey registration/sign-in on supported browsers/devices. Do not infer those from local tests.
