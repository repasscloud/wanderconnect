# UAT image pipeline staging settings

CONFIRMED: [owner instruction #261, 10 November 2026](https://github.com/repasscloud/wanderconnect/issues/261#issuecomment-6742634264). UAT application hosting is **OPEN**, including Vultr; production remains Azure (owner clarification in this chat). This prepares code/configuration only. It does not deploy the API, create Cloudflare resources or enable delivery. The existing media path remains the default.

## UAT service addresses

| Setting | Confirmed value |
| --- | --- |
| Upload Worker (`PUT /uploads`) | `https://images-upload-uat.swoovr.app` |
| Delivery Worker | `https://images-uat.swoovr.app` |
| Processing/Delivery Worker `API_BASE_URL` | `https://api-uat.swoovr.app` |
| Upload Worker `ALLOWED_ORIGINS` | `https://uat.swoovr.app` |

The optional Azure API public origin, `AllowedHosts` and custom-domain binding use `api-uat.swoovr.app` in this PR itself; the Worker handoff uses matching API/upload/delivery addresses. Bind origin TLS for this exact name (or a controlled `*.swoovr.app` certificate), and create `api-uat` DNS plus `asuid.api-uat` verification only if Azure is selected. Vultr requires its selected reverse proxy/load balancer and TLS instead. The adjacent Azure auth/admin hostname values also match D355 so the two branches can be integrated cleanly. Full realm generation, passkey/mobile changes and D355 historical records remain in [PR #305](https://github.com/repasscloud/wanderconnect/pull/305); complete that before the full UAT sign-in rollout.

Cloudflare resources are being prepared separately in `swoovr-cf-workers`; this API change neither creates nor deploys them.

## Portable API runtime configuration

Set these on the **Swoovr.Api process only**, regardless of hosting provider:

| Environment variable | Value or source |
| --- | --- |
| `Media__Pipeline__Enabled` | `true` |
| `Media__Pipeline__ServiceToken` | Private generated secret, at least 32 characters |
| `Media__Pipeline__UploadSigningKey` | Different private generated secret, at least 32 characters |
| `Media__Pipeline__UploadOrigin` | `https://images-upload-uat.swoovr.app` |
| `Media__Pipeline__AcceptedPolicyVersion` | `swoovr-images-2026-11-08-v1` |

Generate independent, cryptographically random service/signing secrets privately. The API service token is the Cloudflare processing Worker's `API_SERVICE_TOKEN`. The upload signing key is the upload Worker's `UPLOAD_SIGNING_KEY`. Their values must match their respective API values; the two secrets must differ. Check `workers/images/README.md` and `CLOUDFLARE-HANDOFF.md` for current Worker names/bindings. Neither secret is the processor inbound API key or a Project Arachnid credential. Never give either to member clients or commit them to Git.

On a Vultr API host, inject these variables into the API container through the chosen runtime's private secret mechanism or an owner-managed environment file outside Git with owner-only permissions (0600). Do not bake secrets into images, log the environment or put values on a command line. An environment file is not automatically uploaded or installed by this repository. Keep separate files/credentials for UAT and production. Do not copy the API secrets into the processor VM files or BFF/mobile configuration.

If the owner chooses the **optional Azure implementation**, `infra/uat/services.bicep` sets the three nonsecret values on `apiEnv`. The manifest `infra/uat/secret-bindings.json` supplies these API-only Key Vault references (physical names use the deployment prefix, default `swoovr-uat`):

| Key Vault secret name | API environment binding |
| --- | --- |
| `swoovr-uat-media-pipeline-service-token` | `Media__Pipeline__ServiceToken` |
| `swoovr-uat-media-pipeline-upload-signing-key` | `Media__Pipeline__UploadSigningKey` |

The existing per-actor managed identity and secret access machinery grants these only to `api`. No secret values appear in Bicep or parameters. Populate the secrets privately before starting the API. Other actors and migration/operator jobs do not receive these bindings. Azure resources are not required for Vultr deployment.

## Migration and acceptance

1. Build/select the reviewed API image and record its immutable digest. Confirm it includes `20261108133228_ImageProcessingStaging`; this migration adds the image processing jobs and callback receipts. It does not promote existing media or make images deliverable.
2. Prepare the target PostgreSQL database, migration identity and recoverable backup using the selected host's deployment procedure. Do not run concurrent migrations.
3. Run the API image with the existing `migrate` argument and migration identity (the API entry point invokes the same migrator on every host). For the optional Azure path use the existing `swoovr-uat-migrate` job from [DEPLOYMENT-UAT.md](DEPLOYMENT-UAT.md). Wait for terminal success, not just a start receipt. Do not print the connection string.
4. Privately verify `__EFMigrationsHistory` contains the migration and `image_processing_jobs` / `image_processing_receipts` exist. Do not expose database/admin access publicly for this check.
5. Supply the five API runtime settings and the ordinary database/auth/hosting settings for the chosen host. Start the API with its runtime identity. Run `/health/live` and `/health/ready` through the actual trusted proxy path; a live process alone does not prove database or provider readiness.
6. Configure the separate Cloudflare resources and Worker secrets through their handoff. Verify invalid/missing service tokens are denied; use only authorised synthetic adult fixtures for upload/callback smoke checks. Verify the exact policy version and staged callback persistence, including retries/conflicts. Keep real credentials and object contents out of evidence.
7. Confirm staged images remain non-deliverable: `ImagePipelineOptions.Deliverable` is hard-coded false, the access endpoint returns `pipeline-cutover-incomplete`, no BFF/mobile upload switch is enabled, and the existing media path remains default. Enabling staging is not authorisation to publish images.

Do not roll back by dropping staged records or migration history. Disable the opt-in API setting and coordinate Worker queue processing if staging must be stopped; preserve callback/lifecycle records for reconciliation. Any future delivery/cutover requires a separately reviewed implementation and acceptance.

## Validation evidence

Local evidence on 10 November 2026:

- `env -u SWOOVR_TEST_DATABASE ./scripts/check.sh` — exit 0; Release build: zero warnings/errors; Node suites: 109 + 32 passed; image pipeline configuration: 13 checks; all three local HTTP smoke stages passed. Persistence checks explicitly skipped (no test PostgreSQL configuration).
- `python3 -m unittest discover -s tests/uat -p test_image_pipeline_settings.py -v` — 2 passed: exact API settings and API-only secret consumers.
- `python3 tests/uat/infrastructure.py --bicep /tmp/swoovr-bicep-d355/bicep-osx-arm64` — 87 compiled infrastructure/security assertions passed with Bicep 0.49.2; no Azure authentication or provisioning.
- `python3 tests/image-pipeline/api_transport.py` — 11 actual local API HTTP checks passed: service authentication, disabled/staging behavior and refusal without persistence. No member/provider integration claim.
- Additional full `tests/uat/test_*.py` discovery: 9 passed, 2 existing realm-export failures caused by the local Zammad client in the baseline. The separate D355/#124 branch excludes that local-only client and passes all 9 realm tests. This staging-settings branch deliberately leaves the realm generator to that PR.

Hosted API/database, migration execution, Cloudflare secrets/bindings and Vultr/Azure deployment remain untested. No credentials or resources have been installed. Full local logs are in ignored `.local/image-staging/`; this committed record is the nonsecret evidence summary.


### PR #306 review follow-up — 10 November 2026

Codex's API hostname finding was valid for this branch independently of #305. Updated the API public origin, host allowlist, custom-domain binding and operator public origin to `api-uat.swoovr.app`. The Worker handoff now uses matching confirmed API/upload/delivery addresses and describes private secrets independently of UAT provider. The adjacent Azure auth/admin host values also match D355; full realm generation, passkey/mobile migration and decision records remain in #305; the API staging address no longer depends on that PR for its binding.

Regression evidence: the new hostname/handoff test failed before the fix, then all 3 focused staging tests passed. Bicep 0.49.2 compilation passed 90 infrastructure/security assertions, including the actual compiled API origin/host allowlist/domain binding. A fresh `env -u SWOOVR_TEST_DATABASE ./scripts/check.sh` exited 0, with zero Release warnings/errors, Node suites 109 + 32 passed and all three local HTTP smoke stages passing. Persistence explicitly skipped without PostgreSQL; no hosted deployment/TLS/DNS acceptance is claimed.
