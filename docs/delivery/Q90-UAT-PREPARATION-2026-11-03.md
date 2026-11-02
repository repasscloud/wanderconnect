# Q90 / issue124 — approved UAT topology, preparation only

CONFIRMED D291 records the owner's hosting answers. The proposed Australia East Container Apps topology, separate Keycloak PostgreSQL database/login, non-development realms, nested hostnames, paid edge/Key Vault origin configuration without Tunnel, isolated backups and naming are approved. This does not provision an Azure subscription, tenant, database, realm, DNS record, paid edge service, secret or deployment.

Existing `infra/uat` templates remain the code-side preparation. The current change derives the registration admission URL from the API origin and gives its private secret only to API/Keycloak actors. No credentials are committed or installed. All74 compiled infrastructure/security assertions and35 synthetic parameter acceptance/refusal checks passed locally using Bicep0.48.1. Logs are private under `.local/overnight-delivery/infrastructure-final.log`; preflight used public synthetic metadata only.

The owner will create/select the subscription and deploy. Operator credentials, effective realms/flows, Key Vault bindings, provider configuration, DNS/TLS/edge, hosted role isolation, backup restore and hosted/device acceptance remain owner-controlled gates. Firebase preparation additionally waits for Q121/#170; guest trusted-contact readers wait for Q123/#173. No cloud authentication or provisioning ran, no hosted state is claimed, and issue124 stays open with owner-action.

Pre-commit integrated `./scripts/check.sh` passed (exit0), including the actual disposable PostgreSQL suite and all three HTTP smoke stages. This is combined-branch validation; issue-specific browser/native/provider gaps remain explicitly identified above.
