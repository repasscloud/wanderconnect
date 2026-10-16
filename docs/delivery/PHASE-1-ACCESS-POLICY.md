# Phase 1 — account access policy slice

16 October 2026. Status: partial Phase 1, ready for local review; not live authentication.

## Intent and requirements

Implement the provider-independent portion of role boundaries while configured Keycloak evidence and Q10 block privileged integration. The seven application projects and existing dependency directions are retained. Domain holds pure rules; executable checks reference Domain. No packages, projects, endpoints, UI changes or workflow changes are introduced.

CONFIRMED (D02–04): Keycloak remains the backend; members may use Apple/Google/email; partner/admin/support require dedicated email/password or email links and cannot use social authentication; MFA is optional. Public name remains replaceable; engineering namespace remains DatingApp. Six schemes and independent System/Light/Dark are unchanged and remain phase 2 work.

DEFAULT (D10): one explicit application role per evaluated session, no role hierarchy, explicit venue membership for venue operations. The earlier separate-staff-account default is superseded by D12. OPEN (Q10): dedicated email has no implemented classification. The policy reserves a future approved server verdict; real adapters must not supply true until the definition and configured evidence are validated. Accounts may support both direct and linked social credentials. Email/password implements member email for this slice. OPEN (Q02): member magic links are still a proposal and therefore denied. Unknown roles/methods, inactive sessions and absent sessions deny access.

## Implemented behaviour and demo

Run `dotnet run --project tests/DatingApp.Checks --configuration Release` for the executable demonstration. Fifty policy cases exercise successful member Apple/Google/password access; hypothetical approved email password/link verdicts for each staff role without mandatory MFA; social/unknown-method denial for staff; missing approved privileged-email evidence; inactive sessions; cross-role denial; missing, empty, revoked and wrong venue scope. Invalid enum values fail closed. A support role never inherits admin access; an admin role does not implicitly gain venue or member access.

The policy is a coarse gate only. A successful member result grants no profile browsing or contact permission; adulthood/identity, block, privacy and other product rules still need their own checks. A successful staff area result grants no arbitrary record access or unrestricted support powers. Resource-specific permissions and audit trails are future work.

## Trust boundary and next integration steps

`AccessSession` is an internal application snapshot, not a public contract. A future server adapter must validate the actual Keycloak session before constructing it. It must obtain trusted login-method evidence from the verified contract, resolve role and venue membership from authoritative application storage, and recheck session revocation/account suspension. `IsActive` represents those checks; the record does not perform them. Evaluate using fresh facts for each protected operation, never retain a successful decision as a durable grant. No request body, client flags, matching email, social profile links or verification badges may construct or elevate this snapshot.

The policy has no MFA input or requirement. This does not implement MFA enrolment/recovery. It also does not implement invitation provisioning, token validation, account linking, expiry, callback replay protection, logout, email delivery or atomic email-link consumption. Its method names do not assert provider capabilities. If Keycloak cannot prove dedicated email authentication, the future adapter must deny staff access.

The API still exposes only its original public foundation routes; no new privileged route or authorization handler exists. Provider callbacks and direct API negative tests must be added with the real adapter, before any protected feature is enabled. No fake JWT validator or login simulation is introduced.

## Remaining phase 1 gates

- Q01: owner supplied official Keycloak docs/source. No existing environment found; version/realm proposal is [documented](../architecture/KEYCLOAK-DEVELOPMENT.md). Session evidence still requires configured sandbox validation.
- Q10: precise dedicated-email definition; question asked. No classifier or exclusive account type is implemented.
- Q02: member email-link approval and provider-supported safe exchange.
- Q03: authenticated web render choice remains OPEN; static SSR shell retained.
- Q09: pin mobile tooling and obtain Android/iOS build and device evidence. No workloads installed locally.
- Implement and validate sandbox login/logout/recovery, provisioning, optional MFA and the full negative matrix in authentication architecture, then typed client and web/mobile session journeys.

Local tests prove application rule behaviour only. Phase 1 is not complete and no deployment is authorized. See [validation](VALIDATION.md) for actual commands and limitations.
