# Staff access implementation plan

**Goal:** Implement #196, #206 and #193 in that order, as separate draft stacked PRs.
**Architecture:** Existing Domain policies, application role grants, API authorization, Infrastructure Keycloak adapters and Admin SSR. Keycloak owns credentials and sessions; no new projects or identity database.
**Spec:** GitHub issues #196, #206 and #193, including the #193 mandatory-staff-MFA clarification; existing Keycloak integration design and D189/D322.

## Constraints and decisions

CONFIRMED: Business is internal and independent; external partner membership never grants staff authority. Staff MFA is mandatory per the current issues, superseding older optional-staff wording. Preserve Entra SSO and Root password + OTP. Only Root appoints Legal officers. No speculative organisation tables or partner portal, production configuration, merges, destructive migrations or Actions changes.
DEFAULT: Use an explicit API-owned workspace projection for navigation; Business has only its authorised home until actual partner operations exist. Reuse existing lifecycle/preservation authority and revocation cutoff. Provider operations fail closed and preserve unrelated credentials/actions. Later branches target their predecessor until merged, then retarget main.

## Review focus

- Business direct API requests must never inherit member, support, legal or platform access.
- Concurrent role/status/revocation changes must not restore access or overwrite unrelated identity settings.
- Provider uncertainty must not make a security operation appear successful.
- Existing non-MFA sessions and credential deletion must not bypass mandatory enrolment.
- Elevation must be bound to the authenticated subject/session and never substitute for application permissions or purpose grants.

## 1 — #196 Business

- [ ] Add failing role/invitation/isolation checks; run and observe failure.
- [ ] Append Business to existing enum, invitation/CLI mapping and narrow staff workspace projection; require MFA for Business; retain existing role gates and ticket queue policy.
- [ ] Use API workspace permissions for Business landing/navigation and refuse unrelated direct portal URLs. No placeholders.
- [ ] Run checks; document endpoint audit, actual evidence and provider configuration gaps; commit/push and draft PR to main; comment on issue.

## 2 — #206 Staff accounts

- [ ] Test internal management authorization, multiple roles, Root/Legal protection, status/revocation and provider failure boundaries.
- [ ] Add existing-realm Keycloak directory/security adapter and API staff listing/detail/create/update/credential/session operations with recent authentication, reasons, confirmation, audit and lifecycle authority.
- [ ] Add Admin SSR listing and management forms with typed client contracts; reuse Zammad sync signal and effective role restrictions.
- [ ] External partner identity foundations stay distinct from internal roles; reject organisation assignments until authoritative organisations exist rather than inventing identifiers.
- [ ] Run checks; document unsupported provider/owner-controlled paths; commit/push and draft PR based on #196; comment on issue.

## 3 — #193 MFA

- [ ] Test mandatory staff assurance, optional/enforced member policy, strictest roles, reset separation and fresh elevation expiry/session binding.
- [ ] Integrate Keycloak-native enrolment/credential removal/session logout and policy enforcement, keeping secrets and OTP verification inside Keycloak.
- [ ] Add member settings and staff controls; distinguish requirement removal from credential clearing; preserve mandatory staff policy after resets, prevent self-reset and audit interventions.
- [ ] Enforce fresh MFA on sensitive operations in addition to existing authorization/purpose grants; retain provider-native recovery and document acceptance limits.
- [ ] Run full checks, review security diff, commit/push and draft PR based on #206; update issues and report merge order/evidence/gaps.
