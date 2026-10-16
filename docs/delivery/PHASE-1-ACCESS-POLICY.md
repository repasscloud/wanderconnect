# Phase 1 — account access policy slice

16 October 2026. Status: partial Phase 1, ready for local review; not live authentication.

## Intent and requirements

Implement the provider-independent portion of role boundaries while Q01 blocks IdentityCloak integration. The seven application projects and existing dependency directions are retained. Domain holds pure rules; executable checks reference Domain. No packages, projects, endpoints, UI changes or workflow changes are introduced.

CONFIRMED (D02–04): IdentityCloak remains the backend; members may use Apple/Google/email; partner/admin/support require dedicated email/password or email links and cannot use social authentication; MFA is optional. Public name remains replaceable; engineering namespace remains DatingApp. Six schemes and independent System/Light/Dark are unchanged and remain phase 2 work.

DEFAULT (D08/D10): separate dedicated staff identities, one explicit application role per evaluated session, no role hierarchy, explicit venue membership for venue operations. Email/password implements member email for this slice. OPEN (Q02): member magic links are still a proposal and therefore denied. Unknown roles/methods, inactive sessions and absent sessions deny access.

## Implemented behaviour and demo

Run `dotnet run --project tests/DatingApp.Checks --configuration Release` for the executable demonstration. Fifty policy cases exercise successful member Apple/Google/password access; dedicated email password/link access for each staff role without mandatory MFA; social/unknown-method denial for staff; missing dedicated-account evidence; inactive sessions; cross-role denial; missing, empty, revoked and wrong venue scope. Invalid enum values fail closed. A support role never inherits admin access; an admin role does not implicitly gain venue or member access.

The policy is a coarse gate only. A successful member result grants no profile browsing or contact permission; adulthood/identity, block, privacy and other product rules still need their own checks. A successful staff area result grants no arbitrary record access or unrestricted support powers. Resource-specific permissions and audit trails are future work.

## Trust boundary and next integration steps

`AccessSession` is an internal application snapshot, not a public contract. A future server adapter must validate the actual IdentityCloak session before constructing it. It must obtain trusted login-method evidence from the verified contract, resolve role and venue membership from authoritative application storage, and recheck session revocation/account suspension. `IsActive` represents those checks; the record does not perform them. Evaluate using fresh facts for each protected operation, never retain a successful decision as a durable grant. No request body, client flags, matching email, social profile links or verification badges may construct or elevate this snapshot.

The policy has no MFA input or requirement. This does not implement MFA enrolment/recovery. It also does not implement invitation provisioning, token validation, account linking, expiry, callback replay protection, logout, email delivery or atomic email-link consumption. Its method names do not assert provider capabilities. If IdentityCloak cannot prove dedicated email authentication, the future adapter must deny staff access.

The API still exposes only its original public foundation routes; no new privileged route or authorization handler exists. Provider callbacks and direct API negative tests must be added with the real adapter, before any protected feature is enabled. No fake JWT validator or login simulation is introduced.

## Remaining phase 1 gates

- Q01: authoritative IdentityCloak source/docs, version, sandbox and trusted login-method evidence. Focused question asked; awaiting owner information.
- Q02: member email-link approval and provider-supported safe exchange.
- Q03: authenticated web render choice remains OPEN; static SSR shell retained.
- Q09: pin mobile tooling and obtain Android/iOS build and device evidence. No workloads installed locally.
- Implement and validate sandbox login/logout/recovery, provisioning, optional MFA and the full negative matrix in authentication architecture, then typed client and web/mobile session journeys.

Local tests prove application rule behaviour only. Phase 1 is not complete and no deployment is authorized. See [validation](VALIDATION.md) for actual commands and limitations.
