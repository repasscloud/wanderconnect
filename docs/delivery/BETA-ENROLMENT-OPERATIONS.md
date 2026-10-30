# Beta enrolment operations

D273 DEFAULT implements the Admin operations and iOS record parts of25 under CONFIRMED D62/D76/D91. D275 adds [invitation queue and expiry](BETA-INVITATIONS-AND-EXPIRY.md); the evidence below describes the original D273 scope. Completed private distribution still requires the owner's external Apple channel. No live invitation or provider verification is claimed.

## Staff workflow and contract

`/beta` is a server-rendered Admin page. Existing add/list/revoke endpoints remain authoritative and the list is explicitly latest1000, including revoked records. Normalized duplicate live addresses remain idempotent. Revoke requires a separate unchecked confirmation. Current backend Admin/root authorization applies even to handcrafted requests; support/member callers never receive the private list or mutation rights. API list and staff SSR use no-store. Forms use antiforgery and keep access tokens server-side.

The new stable `PUT /api/admin/beta/{id}/ios-distribution` uses the common Swoovr-Version header and request `{state,expectedRevision,confirm}`. States: not-recorded, not-invited, invited, accepted, removed. The current Admin/root must have a fresh session. Source actor/time are server-owned; state/revision and the address-free audit event commit together. Stale concurrent forms conflict, identical current state is a no-op, exhausted/unsupported/unconfirmed updates refuse. No Apple API is called. The response source is explicitly `staff-recorded-not-provider-verified`; an iOS record neither activates web access nor bypasses any other account gate. Revoking an enrolment does not remove an external TestFlight/private-distribution invitation: staff perform that action externally and record its result separately.

Additive migration defaults put every previous enrolment at not-recorded/revision0 with no invented actor/time. Linked enrolments appear in their owner's export, omitting staff actor IDs, and in separately encrypted account evidence. Ordinary final account deletion already erases linked rows. Unlinked/historical records require separate privacy review rather than assuming email proves historical identity. Invitation dispatch and inactive/unlinked record expiry follow-ups remain tracked under25/118; do not close25 based only on this slice.

## Current beta access on live message content

An actual existing SignalR listener demonstrated the old revocation gap: HTTP access was removed but a new message body still arrived on the old connection. All `message` event publishers now pass through `BetaMessageAudience`, including text, farewell/system projections, late screened photos and staff tombstones. Current active member-realm beta authorization is read from the database for every event; an old connection/token is insufficient. Message notifications use the same guard before creating/updating centre or dispatching email/push. The public-launch Beta:Enabled=false switch disables this beta-specific gate.

Beta removal is not a block, termination or ending of consent. Existing relationships and retained history remain, and an eligible counterpart can keep sending; a removed recipient cannot fetch or receive new message content until their beta access is restored. Neutral closure/account/safety notices and the existing data/deletion rights are preserved. No other member is told the recipient's private beta status.

## Evidence

The browser suite exercises real local Keycloak/API/Admin/Web with fictional adults, an actual live socket before/after revocation, roles, antiforgery, normalization, activation, CAS updates and retained account rights. PostgreSQL checks upgrade an already-populated old schema and test metadata, races, own export and encrypted preservation. Screenshots contain fictional addresses only. Exact commands/results and gaps are in VALIDATION.md. External Apple operations, actual email delivery and native OS UI are not established by these tests.
