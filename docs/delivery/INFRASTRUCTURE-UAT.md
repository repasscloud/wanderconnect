# UAT infrastructure preparation

D283 DEFAULT implements the reversible Australia East choice in D221/Q90 (#124), still awaiting the owner hosting answer. These Bicep files have been compiled locally, not provisioned, Azure-validated or cost-approved. The owner creates/selects the subscription. No Actions are enabled.

`infra/uat/foundation.bicep` prepares a Consumption Container Apps environment, VNet, delegated private PostgreSQL subnet/DNS, private Key Vault and backup Blob endpoints/DNS, authenticated ACR, infrastructure logs and dedicated managed identities. `services.bicep` prepares PostgreSQL17 and staged applications/jobs. All service, job, secret-grant, invitation and schedule switches default off. PostgreSQL creation still occurs in the services stage; inspect/approve an Azure what-if before deployment. ACR uses the documented2025-06-01-preview contract to explicitly disable anonymous pulls and select ordinary AcrPull role semantics. This is a deployment dependency, not a general preview upgrade policy.

UAT is one replica per serving application, single revision, single region, without database HA. Coordinated maintenance may require members to sign in again; BFF sessions and SignalR are currently in memory. This is a bounded UAT DEFAULT, not a production availability claim. Database native backups retain30 days; the separate encrypted daily/weekly/monthly jobs retain the approved schedule through the D261 publisher/expiry/recovery identities. The backup account origin has no trailing slash, matching the actual managed-identity client contract.

`secret-bindings.json` contains names and permitted consumers, never values. Runtime secret references use each actor's managed identity and secret-scoped Key Vault Secrets User grants. Logical crypto key1 bindings require exact physical Key Vault versions; retain historical key material through rotation/recovery. Only the recovery identity receives the age private identity. Publisher cannot decrypt old age generations; it can read current sealed media using the evidence key to produce a verified current-state bundle. Secret access is not evidence that credentials are correct or a provider accepted a request.

Deploy in explicit stages after owner approval: foundation; populate/validate private inputs; services with secret access only; database bootstrap; migration; identity/bootstrap; serving; acceptance; schedules/invitations. The operator image and full deployment commands are subsequent original-queue work. Do not run these templates as a substitute for the final deployment runbook. Root-admin job deliberately has no subject and refuses execution until the operator supplies the approved Entra staff subject. Job retries are0 to avoid blindly replaying irreversible external work.

Key Vault public access is disabled by default. An approved operator can temporarily supply exact bootstrap IPv4 CIDRs, using their own RBAC identity; close the exception after input/bootstrap work or use approved private access. ARM secure-parameter retrieval is explicitly enabled; subscription deployment permission and vault deployment action remain operator prerequisites. No access keys, SAS or secrets belong in parameter files. ACR is an authenticated public endpoint, with admin/anonymous access disabled; artifact push permission is distinct from runtime AcrPull.

Before deployment run `python3 scripts/uat/preflight.py <nonsecret-services-parameters.json>`. The file may be an ARM parameter wrapper or plain parameter object. Supply immutable ACR digest references for all six images, all ten physical crypto versions, foundation environment domain, exact trusted proxy CIDRs, private R2/backup origins and age public recipient. The preflight rejects tags, missing physical versions, trust-all proxy ranges and credential-bearing/ambiguous origins. It does not contact Azure, inspect remote image architecture, inspect secret contents, prove proxy reachability or approve deployment. Do not put secret values in this file.

The screening service is private ingress with2CPU/4Gi and its existing entrypoint. Its startup command refuses missing API/Shield credentials. The existing separately owned moderation image must be built for linux/amd64 and reviewed; the local arm64 image is not the Azure artifact. D201's approved Shield provider/agreement is settled. API hosted screening requires the known-content check. Didit UAT configuration explicitly selects live mode; approved live workflow/webhook/identity/legal operational acceptance remains necessary before real testers. Push stays explicitly disabled until real device credentials/acceptance are available, never simulated.

Validate locally:

```
python3 tests/uat/infrastructure.py --bicep /path/to/bicep
python3 tests/uat/preflight.py
python3 tests/uat/app_images.py
```

These compile every module and exercise security/parameter contracts and isolated published hosts. They are not Azure deployment/what-if, DNS/TLS, real provider, Entra or end-to-end hosted acceptance. No browser suite is required by these non-UI templates; the published artifact fixture separately checks SSR/static resources and command readiness.
