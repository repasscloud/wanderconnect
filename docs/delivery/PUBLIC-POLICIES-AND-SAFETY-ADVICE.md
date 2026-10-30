# Public policies, acceptance preparation and safety updates

D269 DEFAULT under D215/D220/D264, issues113/121. Legal57 remains OPEN. The supplied terms/community text is DRAFT, pending legal review, not in force. No signatory status, verified Code compliance or legal approval is created.

## Public readers and first use

`/terms` and `/community-guidelines` render the complete D264 draft, linked from Me, Safety & Support and the registration theme source. Shared Client content preserves its source Markdown hash; web/native readers explicitly say reading does not accept or activate it. The registration checkbox remains the confirmed adult attestation. Its old agreement sentence is replaced in the theme source; no existing sign-in/age/email clock is backfilled as legal acceptance.

`/safety/advice` and its offline native reader share connection and meeting advice. Me shows it before setup, Profile retains it, and meeting safety gives advice before planning. It includes consent, boundaries, public meeting places, independent travel, trusted contacts, appropriate help and clear limits on badges/alerts/emergency monitoring, without blaming a person harmed. No advice acknowledgement/dismissal analytics or forced consent checkbox is added.

The shared running local Keycloak still mounts MAIN's theme. Registration source checks are separate from actual new-theme browser acceptance; verify the packaged UAT theme before deployment. Public web/member flows use the worktree during owned tests and are restored afterwards.

## Approved versions and explicit acceptance

The API-owned `ApprovedPolicyCatalogue` starts empty. Only a reviewed code change can supply approved text, immutable version/digest and effective time. Setting `Policies:RequiredVersion` cannot approve a draft: unknown/unapproved/non-effective selections fail API startup. With no selection, existing account/beta/adult/age/phone gates remain unchanged; no acceptance record is fabricated.

Stable endpoints: GET `/api/policies/{version}` exposes only effective approved public text; GET `/api/me/policies` returns the authenticated member's requirement/status; POST `/api/me/policies/accept` accepts only `{version, confirm:true}` for the exact selected version. Account, digest, status and time are server-owned. Replays preserve the original timestamp; new versions append. Reads/writes use no-store. The web/native review step links/renders the exact approved source, leaves consent unchecked and records it only on explicit submit. No real approved entry is installed; approved-mode tests inject fictional text in the owned service fixture.

Member access is checked server-side. Required policy review does not remove an account restriction. Account summary, deletion/export, notices, appeals, safety/report routes and acceptance itself retain their existing rights/exemptions. Acceptance records are exported only to their owner, captured under holds and erased with final ordinary account deletion; independently preserved copies follow their existing clocks. Database guards prevent rewriting history or premature erasure. Directly named active holds require encrypted acceptance copies to be decrypted and hash-checked before external identity deletion and again after provider work; ordinary deletion then completes while independent copies remain, without waiting for release. Missing copies or unknown coverage stop before provider work. Native essential controls remain visible without acceptance and without an automatic redirect loop.

## Centre-only safety publications

`PolicyNotices:Enabled` defaults false. Notice sources are currently `/safety` and `/safety/advice`; privacy/terms/community drafts are refused. Adding a legal-policy broadcast source requires legal approval and a reviewed allowlist change. Publication is a separate operator action, never startup/login/page reading. The CLI retains its existing database-operator trust boundary; a current named root account supplies audit provenance, not a new SSO session.

With the normal operator environment/secrets and an actual current root staff account ID:

```sh
dotnet src/Swoovr.Api/bin/Release/net10.0/Swoovr.Api.dll policies notices stage 2026-11-01.1 "Public safety guidance update" /safety/advice --operator ROOT_ACCOUNT_ID
dotnet src/Swoovr.Api/bin/Release/net10.0/Swoovr.Api.dll policies notices publish PUBLICATION_ID --operator ROOT_ACCOUNT_ID --source-published true
dotnet src/Swoovr.Api/bin/Release/net10.0/Swoovr.Api.dll policies notices process PUBLICATION_ID
```

Stage/inspect first. Publish only after reviewing the actual already-public source; it snapshots current active members once and does not deliver. Process completes at most100 targets per call; repeat until0. Restarts/replicas cannot duplicate or enroll newly registered members into an old snapshot. Current inactive/barred recipients are suppressed. Source/checkpoint/centre row commit together; no push/email is queued, irrespective of external-channel settings. The member projection requires their own completed target, valid immutable source and matching dedupe metadata. Native navigation accepts only the two supported destinations.

DEFAULT delivery window30days, grouped recipient/centre/publication-operator metadata expiry60days. Unpublished staging also expires after60days. Current holds, unknown coverage or retained copies postpone personal group expiry. Disposal is transactional; a non-member version disposition prevents reuse without retaining operator/member identifiers in that row. Ordinary audit has its separate retention rules. Member exports include only their own minimal delivery receipt. Draft privacy0.11 discloses these purposes/clocks.

## Evidence and remaining gates

Actual commands/results, initial fixture failure, screenshots and platform gaps are in VALIDATION.md. All identities/approval fixtures are fictional. Physical native UI, actual legal approval/catalogue activation, new-theme hosted registration, public policy publication and live provider delivery are not established by local builds. No merge, deployment or Actions activation is authorised.
