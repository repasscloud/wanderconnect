# UAT deployment operator handoff

D286 DEFAULT. **Preparation only: no deployment, merge, registry push, subscription purchase, DNS change or Actions activation has occurred.** The owner must explicitly approve deployment. Q90/#124 selects subscription, billing, region/topology and Cloudflare certificate options; this runbook implements reversible D221 Australia East defaults. Legal57, real credentials, staff/operational coverage and hosted acceptance remain admission gates. This document is the endpoint for the original preparation queue, not authority to admit testers.

## Before approval-time execution

The owner supplies/selects the Azure subscription and deployment operator (resource deployment plus role assignment permissions, registry push permission, Key Vault input/deployment access), confirms Australia East/SKUs/cost/residency, and approves the UAT resource group `rg-swoovr-uat-aue`. Use product=Swoovr/environment=uat/owner=RePassCloud tags. No agent creates a subscription. Verify required provider registrations (`Microsoft.App`, `Microsoft.OperationalInsights`, `Microsoft.Network`, `Microsoft.ManagedIdentity`, `Microsoft.KeyVault`, `Microsoft.ContainerRegistry`, `Microsoft.DBforPostgreSQL`, `Microsoft.Storage`, `Microsoft.Insights`) and quotas through the approved operator.

Have Azure CLI/Bicep, Docker/buildx, Python3 and Edge/Playwright available. Run from the reviewed commit/worktree, never develop in MAIN. Retain the complete nonsecret parameter inventory and image/package digests. Never place credentials in commands, parameter files, images, Git, screenshots or transcripts. Runtime secrets are Key Vault references. Use portal/private secret input tooling; disable shell tracing and avoid secret-valued `az --value` arguments. No `.local` configuration is a hosted secret source.

Create private operator scratch outside Git; only nonsecret deployment metadata/template overrides belongs here:

```sh
UAT_WORK="$(mktemp -d "${TMPDIR:-/tmp}/swoovr-uat-operator.XXXXXX")"
chmod 700 "$UAT_WORK"
UAT_RG=rg-swoovr-uat-aue
UAT_SUBSCRIPTION=OWNER_SELECTED_SUBSCRIPTION_ID
az account set --subscription "$UAT_SUBSCRIPTION"
az group create --name "$UAT_RG" --location australiaeast --tags product=Swoovr environment=uat owner=RePassCloud
az deployment group what-if --resource-group "$UAT_RG" --template-file infra/uat/foundation.bicep
# Review/approve the actual plan before the next command.
az deployment group create --name swoovr-uat-foundation --resource-group "$UAT_RG" --template-file infra/uat/foundation.bicep --query properties.outputs --output json > "$UAT_WORK/foundation.outputs.json"
```

Foundation prepares private database/vault/backup networking, Container Apps environment, registry, identities and infrastructure logs. A deployment operator needs approved private access to Key Vault, or an explicit temporary IPv4 CIDR bootstrap exception in foundation parameters; default public access is disabled. Close any exception after inputs/bootstrap. Bootstrap grants and template secret retrieval still require appropriate RBAC; do not weaken the vault to make an unauthorised operator work. [Topology and limits](INFRASTRUCTURE-UAT.md).

## Images, private inputs and database

After approval, derive `UAT_REGISTRY` from foundation's registryServer. Build and push the reviewed six linux/amd64 artifacts; registry publication is separate from deployment approval if the owner scopes it separately.

```sh
UAT_REGISTRY=OWNER_FOUNDATION_REGISTRY.azurecr.io
UAT_TAG=REVIEWED_COMMIT_ID
az acr login --name "${UAT_REGISTRY%%.*}"
for UAT_COMPONENT in api web admin operator; do
  docker build --platform linux/amd64 -f containers/uat/Dockerfile --target "$UAT_COMPONENT" -t "$UAT_REGISTRY/$UAT_COMPONENT:$UAT_TAG" .
  docker push "$UAT_REGISTRY/$UAT_COMPONENT:$UAT_TAG"
done
docker build --platform linux/amd64 -f containers/uat/Keycloak.Dockerfile -t "$UAT_REGISTRY/keycloak:$UAT_TAG" .
docker push "$UAT_REGISTRY/keycloak:$UAT_TAG"
# The owner-approved existing screening source; review its source/revision/build context first.
docker build --platform linux/amd64 --target runtime -t "$UAT_REGISTRY/screening:$UAT_TAG" /OWNER/PATH/image-moderation-api
docker push "$UAT_REGISTRY/screening:$UAT_TAG"
```

Resolve all six pushed manifest digests, inspect linux/amd64/runtime users and record them. Use only `registry/repository@sha256:...` in services parameters. Local screening source is a separate repository; its artifact provenance and runtime known-content readiness must be reviewed, not inferred from a local arm64 tag. No image contains actual Shield/provider credentials. [Container evidence](CONTAINERS-UAT.md), [operator tools](OPERATOR-JOBS-UAT.md).

First populate the PostgreSQL administrator password and generated crypto/provider/bootstrap inputs. Database connection strings are populated after the first database-only deployment returns its actual FQDN, before any secret grants/jobs/serving. Complete every named vault input from `infra/uat/secret-bindings.json`, prefixed `swoovr-uat-`. This public manifest specifies exact environment names and permitted consumers. It includes separate database connection strings/passwords (API `swoovr_api`, migration `swoovr_migrator`, operator `swoovr_operator`, backup `swoovr_backup`, identity `swoovr_keycloak`), temporary PostgreSQL/master bootstrap inputs, confidential web/member-admin/Entra secrets, MailerSend/Twilio/R2/Shield/Didit, distinct primary/backup safety escalation mailboxes, and all crypto/recovery keys. Connection strings use the actual private PG FQDN, database, matching login and **SSL Mode=VerifyFull;Root Certificate=/etc/ssl/certs/ca-certificates.crt**. `KC_DB_URL` separately references the Keycloak image trust bundle. Generate strong independent database passwords meeting the bootstrap's printable ASCII32-character minimum.

Persist ten logical crypto key1 values with exact physical Key Vault versions. Encryption/HMAC keys that require base64 must decode to32 bytes; founder/phone secret format follows their documented configuration. Use `keys status`/startup validation, retain historical versions through rotation/backups and never overwrite history to make startup pass. The public age recipient is nonsecret; only recovery identity gets the private age identity. No publisher receives it. [Founder custody](FOUNDER-UAT-PREPARATION.md), [backups](BACKUP-RESTORE.md).

Render disabled realms outside Git using the [Keycloak generator](KEYCLOAK-UAT.md) with real nonsecret Entra tenant/application IDs and exact portal origins. Store the three unexpanded JSON templates in the realm vault inputs. Keep `${SWOOVR_UAT_...}` placeholders; the operator expands secrets only in memory. Provide the origin certificate secret and Cloudflare edge coverage as [EDGE-UAT.md](EDGE-UAT.md) specifies. No private certificate value belongs in parameters.

Copy `infra/uat/services.parameters.example.json` to private operator scratch, replace all placeholders from foundation/approved inputs and retain it as the nonsecret inventory. Fill all immutable image digests, private service origins/bucket, exact crypto versions and trusted comma-separated proxy CIDRs. Leave serving, identity, jobs, hostname binding, invitations and schedules off initially. The sample deliberately fails preflight until complete.

```sh
cp infra/uat/services.parameters.example.json "$UAT_WORK/services.parameters.json"
# Edit only nonsecret metadata; no credentials.
python3 scripts/uat/preflight.py "$UAT_WORK/services.parameters.json"
az deployment group what-if --resource-group "$UAT_RG" --template-file infra/uat/services.bicep --parameters "@$UAT_WORK/services.parameters.json" grantSecretAccess=false
az deployment group create --name swoovr-uat-database --resource-group "$UAT_RG" --template-file infra/uat/services.bicep --parameters "@$UAT_WORK/services.parameters.json" grantSecretAccess=false --query properties.outputs --output json > "$UAT_WORK/database.outputs.json"
# Populate the four database connection secrets using that actual host; all manifest inputs must now exist.
az deployment group create --name swoovr-uat-secret-access --resource-group "$UAT_RG" --template-file infra/uat/services.bicep --parameters "@$UAT_WORK/services.parameters.json" grantSecretAccess=true
# After secret access propagation: create jobs, still no schedules/serving/invitations.
az deployment group create --name swoovr-uat-jobs --resource-group "$UAT_RG" --template-file infra/uat/services.bicep --parameters "@$UAT_WORK/services.parameters.json" grantSecretAccess=true createJobs=true
az containerapp job start --name swoovr-uat-db-bootstrap --resource-group "$UAT_RG"
# Wait for Succeeded, inspect fixed/private job diagnostics; do not proceed on a start receipt alone.
az containerapp job execution list --name swoovr-uat-db-bootstrap --resource-group "$UAT_RG" --output table
az containerapp job start --name swoovr-uat-migrate --resource-group "$UAT_RG"
# Wait for Succeeded, then seed public places (required for real profile cities/discovery).
az containerapp job start --name swoovr-uat-places-import --resource-group "$UAT_RG"
```

Wait for each job's terminal success before the dependent next command. `places-import` downloads only the three fixed HTTPS GeoNames public datasets, prints public input SHA256 values, extracts one bounded file privately and invokes the existing importer under the migration identity. Record provenance/counts/CC BY4.0 attribution. No fictional sample seed is a live global place catalogue. If a public download or import fails, it fails explicitly; no placeholder city fallback.

## Identity, hostname binding and real providers

Deploy identity with jobs on, other service/schedule/invitation flags off. The master bootstrap password is temporary infrastructure access, not staff application authentication:

```sh
az deployment group create --name swoovr-uat-identity --resource-group "$UAT_RG" --template-file infra/uat/services.bicep --parameters "@$UAT_WORK/services.parameters.json" grantSecretAccess=true createJobs=true deployIdentity=true
az containerapp job start --name swoovr-uat-keycloak-bootstrap --resource-group "$UAT_RG"
```

Wait for success; verify all three realms are disabled, nondev, exact callbacks/RP and the member service-client roles/scopes. Existing realms cause refusal, not overwrite. Resolve partial imports privately before any retry. Protect/enrol MFA on the authorised infrastructure administrator and retire the temporary bootstrap credential/configuration after setup. Master administration is distinct from the single Swoovr root account.

Provision/bind the approved origin certificate through `infra/uat/edge.bicep` using its physical certificateSecretVersion. Read the certificateId output and add it to the four `certificates` entries in services metadata. Initially create serving apps with bindPublicHostnames=false, then obtain each actual FQDN/domainVerificationId and create the exact DNS records in EDGE-UAT.md. Set binding true only after certificate/ownership/edge coverage is ready. A full-zone Universal SSL default alone does not cover nested hosts. Use Full(strict), no personalised cache and no Tunnel dependency by default. Preserve corporate-zone settings and approve any paid edge feature separately.

```sh
UAT_CERTIFICATE_VERSION=OWNER_REVIEWED_PHYSICAL_VERSION
az deployment group create --name swoovr-uat-edge --resource-group "$UAT_RG" --template-file infra/uat/edge.bicep --parameters certificateSecretVersion="$UAT_CERTIFICATE_VERSION" --query properties.outputs --output json > "$UAT_WORK/edge.outputs.json"
az deployment group create --name swoovr-uat-serving --resource-group "$UAT_RG" --template-file infra/uat/services.bicep --parameters "@$UAT_WORK/services.parameters.json" grantSecretAccess=true createJobs=true deployIdentity=true deployServing=true
# Read actual metadata for each app; configure DNS/certificates as documented, then update the inventory flags.
az containerapp show --name swoovr-uat-web --resource-group "$UAT_RG" --query '{host:properties.configuration.ingress.fqdn,verification:properties.customDomainVerificationId}'
python3 scripts/uat/preflight.py "$UAT_WORK/services.parameters.json"
# With deployIdentity/deployServing/createJobs/grantSecretAccess true and bindPublicHostnames=true in the complete reviewed inventory:
az deployment group what-if --resource-group "$UAT_RG" --template-file infra/uat/services.bicep --parameters "@$UAT_WORK/services.parameters.json"
az deployment group create --name swoovr-uat-bind --resource-group "$UAT_RG" --template-file infra/uat/services.bicep --parameters "@$UAT_WORK/services.parameters.json"
```

For Entra, create/use the owner-approved staff application in the supplied tenant, restrict assignment to authorised staff, register `https://auth.uat.swoovr.app/realms/swoovr-staff/broker/entra/endpoint`, and inject its client secret through Key Vault. Staff use Entra only; no social/local-password fallback. Verify the actual authorised staff subject and email through the owner procedure; trustEmail=false does not infer verification from an address match. Test first-broker, refresh, logout, removed assignment and no accidental account linking. Members use `swoovr-web`/mobile callbacks; venue and staff never use social sign-in. Passkey RP is uat.swoovr.app; physical/browser acceptance is separate.

Configure MailerSend's verified domain and required DNS through its approved account; API transactional sender and Keycloak HTTP SPI use explicit private inputs, no SMTP fallback. Prove registration/verification/reset and transactional email in controlled mailboxes, recording provider acceptance separately from actual receipt. Configure Twilio owner number/credentials at deployment only; test actual OTP delivery/expiry/rate limits and consent. Never enable simulated SMS/push in UAT. Push remains explicitly disabled until #127 credentials and real-device evidence are supplied; then select the real providers and rerun physical acceptance.

Didit live UAT workflow: explicit TestMode=false, correct workflow/contract, privacy/retention/appeal acceptance and signed webhook `https://api.uat.swoovr.app/api/webhooks/didit`. Use provider-owned payload/signature/replay tests; no `/v1` Swoovr route. Shield credentials/agreement are already approved D201: inject them into private screening and verify actual known-content engine readiness/verdict handling. Do not relax known_content_checked or label local/fake scans live. R2 is private; API-authorised delivery only. Ensure both safety escalation recipients and support channels are staffed; do not publish an SLA.

Enable realms manually only after the corresponding provider/staff checks and admission approval. Sign the approved staff subject in once, then grant the single root via the operator job override below. Do not grant root merely because an email address matches.

## One-shot commands, schedules and admission

The named jobs run `migrate`, `root-admin grant <staff subject>`, `accounts purge`, `founders allocate`, database/Keycloak bootstrap, places import, encrypted API/Keycloak captures, expiry and manual recovery. Each start receipt is not completion. Read terminal status and private diagnostics before retrying, especially externally effective commands. Root job without a subject deliberately refuses.

Prepare a nonsecret root override from the current job template (never retrieve a secret value):

```sh
STAFF_SUBJECT=OWNER_VERIFIED_KEYCLOAK_STAFF_SUBJECT
az containerapp job show --name swoovr-uat-root-admin --resource-group "$UAT_RG" --query properties.template --output json > "$UAT_WORK/root-template.json"
python3 scripts/uat/job-template.py --container swoovr-uat-root-admin -- root-admin grant "$STAFF_SUBJECT" < "$UAT_WORK/root-template.json" > "$UAT_WORK/root-start.json"
az containerapp job start --name swoovr-uat-root-admin --resource-group "$UAT_RG" --yaml "$UAT_WORK/root-start.json"
az containerapp job start --name swoovr-uat-accounts-purge --resource-group "$UAT_RG"
az containerapp job start --name swoovr-uat-founders --resource-group "$UAT_RG"
```

BetaAnalytics and PolicyNotices remain explicit default-off controls. Enable internal aggregates only after privacy/legal review; publish only approved safety sources through the documented notice workflow, never draft legal-policy broadcasts. See BETA-SCORECARD.md and PUBLIC-POLICIES-AND-SAFETY-ADVICE.md.

Use the actual Entra-backed Admin beta-enrolment workflow to admit only approved testers and inspect audit/standing/support/holds. The separate founder-benefit commercial model remains deferred D90. Do not fabricate subscriptions/discount delivery. Validate privacy/export/deletion, accessibility release checks and legal-approved text before admission; intended Code adoption is not signatory or verified compliance. #119 governance requires named accountable people, staff training/coverage, LE contact/agreements and reviewed registers; software templates do not appoint them.

Before schedules: manually run both backup jobs and expiry, verify all three tier commit markers and separate publisher/expirer/recovery permissions, prove a restore drill, and configure alerts/operational ownership. Enable schedules only after these pass by changing enableSchedules=true in the complete services inventory and reapplying with review/what-if. UTC jobs: founders02:00, API backup02:15, Keycloak02:45, expiry04:15. Single completion/parallelism1 and no automatic replica retries do not by themselves prevent operator-created overlapping executions: prohibit overlap per role, alert on failure/missing daily generations, retain actual telemetry and bounded logs. Set Azure Monitor alerts/action recipients for failed jobs, stale backup markers, unhealthy readiness, safety outbox failures and certificate expiry. Record alert drills; no monitoring success is claimed from IaC compilation.

Daily backups retain30 days, weekly4 weeks, monthly12 calendar months. Local encryption is not live replication or an RPO promise. PostgreSQL native30-day backups complement rather than replace the isolated encrypted media-inclusive capture. [Exact capture/recovery policy](BACKUP-RESTORE.md).

## Restore drill and rollback

Run D261's complete paired quarantine/current-authority/media/hold/deletion/session reset procedure before testers. Use the recovery job image/identity with private ephemeral scratch, explicit selected generation IDs and two fresh `swoovr_restore_<32hex>` targets. Run the whole scratch-dependent sequence in one manual recovery execution or an approved private persistent operator environment; separate ephemeral job executions do not preserve files. The default recovery-help job restores nothing.

Stop source Api/Keycloak and all source mutation jobs through the authorised control plane before recovery authority capture; verify stopped state. The job has no control-plane right to stop them. Keep source stopped/fenced. Download encrypted generations with `/opt/backups/azure_store.py` using the recovery MI, and execute `/opt/backups/backup.py` commands from BACKUP-RESTORE.md with `--api-dll /app/Swoovr.Api.dll` and `/opt/uat/bundle-producer` for API authority. All required historical keys must remain available. Source API current-authority capture needs its read-only source connection; inject the named backup connection reference, never construct credentials in a public override. For reconciliation select a separate private recovery R2 bucket/approved credentials; do not perform the drill against a concurrently served media bucket. Current authority must still be available/frozen/fresh; otherwise keep targets fenced and escalate.

After paired receipts, run the concrete operator command `recovery-permissions <API_TARGET> <KC_TARGET>` in the recovery image/identity before any serving switch. It requires distinct owned restore names, ready API/Keycloak receipts with the same operation and verified hosted TLS; unsupported custom routines require separate review. Both sources/mutation jobs must stay stopped. Then verify the permissions below. Restore uses --no-owner/--no-acl, so the restoring administrator initially owns restored objects. Under that approved administrator, for the selected API target transfer only public/restricted table/sequence/view ownership to swoovr_migrator, grant API/operator data permissions and backup SELECT/default privileges as in database bootstrap; retain the operator-owned swoovr_operations schema and grant only USAGE plus SELECT on restore_guard to swoovr_api. For the selected Keycloak target transfer only public application objects/schema ownership to swoovr_keycloak and restore backup read/default grants. Never use unqualified REASSIGN OWNED against the shared administrator: it also affects shared objects. Verify app login cannot mutate either fence or create roles/schema, API readiness accepts the paired ready receipt, and Keycloak can start/migrate under its own login. This Azure-role/ownership acceptance is part of the mandatory hosted drill, not evidence supplied by local fixtures.

Point isolated test revisions at the reconciled targets/recovery bucket, keep public ingress/invitations off, run the core smoke plus held/deleted-data, old credentials/session/push refusal and authority receipt checks. Record actual recovery duration/provenance and approval. Promotion/DNS changes require owner approval. Rollback to previous image digests is allowed only with schema compatibility and the same currently authoritative data; stop workers, restore the reviewed complete parameters/digests and recheck health. Never clear a restore fence, restart divergent sources or replay an old database to make a rollback appear successful. A migration/data rollback requires the paired reconciled restore process, not an image-only command.

## Smoke and final admission gate

After approved deployment, use two fresh approved participants and a private operator terminal. Real registration/email/phone/identity/policy steps stay in the normal browser/provider, with truthful adult details and mutually compatible visible Friends profiles in the same market. Local repository fixtures are fictional adults@example.test and are never presented as real-delivery evidence. Do not try to send real mail to example.test or substitute provider simulation to make UAT pass.

```sh
SWOOVR_PLAYWRIGHT_PATH=/Applications/ChatGPT.app/Contents/Resources/cua_node/lib/node_modules/playwright node scripts/uat/smoke.cjs --base-url https://uat.swoovr.app
```

The guided script requires an explicit RUN acknowledgement, opens two fresh owned browser contexts, observes registration submissions and checks verified/member/required-phone/profile state, then performs mutual discovery, request, acceptance, fictional test message receipt, a clearly marked test report with block and access/send refusal through the BFF/date contract. It saves no credentials/tokens/IDs/response bodies/screenshots/traces/HAR/storage state. Human/provider mailbox receipts remain explicit checkpoint evidence; it cannot certify a real Didit decision from a button press. Bounded30-minute/interruption cleanup; no unattended credential file. Do not admit other testers until both this actual hosted journey and remaining admission gates pass. Test reports/accounts follow ordinary audited retention/deletion, never direct evidence erasure.

Also smoke all four TLS/health origins, CSP/WASM, staff Entra/root/support, private media quarantine/Shield, real email/SMS, web SignalR close-on-block, mobile UAT sign-in/logout, export/deletion/holds, meeting check-in escalation and token privacy, actual human accessibility checks and alerts/paired restore. Liveness200 is not provider or member acceptance; unavailable readiness is a stop. Record fixed outcomes and restricted evidence separately, preserving consent/private-data rules. APNs/FCM delivery remains unverified until credentials/devices exist; disabled push is clearly labelled.

The final STATUS/PR handoff records local commits/evidence and all remaining owner gates. Approval-time execution is an operator task; this runbook makes no deployment claim.

Primary operator references: [Container Apps jobs/overrides](https://learn.microsoft.com/en-us/azure/container-apps/jobs#start-a-job-execution-on-demand), [Key Vault origin certificate](https://learn.microsoft.com/en-us/azure/templates/microsoft.app/2025-07-01/managedenvironments/certificates), [Keycloak26.8.1 Admin REST](https://www.keycloak.org/docs-api/26.8.1/rest-api/index.html). See EDGE-UAT.md for verified DNS/TLS constraints and BACKUP-RESTORE.md for the fenced restore protocol.
