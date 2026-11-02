# Q111 — scoped account inspection

2 November 2026. Implements CONFIRMED [D294](DECISIONS.md), the owner's 156.1–156.5 answers [posted on #155](https://github.com/repasscloud/wanderconnect/issues/155#issuecomment-6572427017) for [Q111/#156](https://github.com/repasscloud/wanderconnect/issues/156). This does not resolve Q110's phone search/account-browser permissions.

## Staff boundaries

Every authorised staff role, including Legal, can read calculated numeric age and DOB assurance from `/api/admin/accounts/{id}/assurance`. The existing account summary also gains numeric `age`, independently of its existing assurance state. Missing DOB gives no calculated age. No ordinary summary or public preview returns the full date.

Full current profile DOB requires a purpose and a live elevation through the existing 60-minute staff-access mechanism. DEFAULT capability name: `profile-dob-open`. Every staff role can request that narrowly scoped capability; it neither grants `identity-open` nor decrypts D191 fields. The separate `POST /api/admin/accounts/{id}/date-of-birth` records purpose/actor/subject in the access log and audits IDs only. Root does not gain Legal identity or historical-DOB decryption. Existing conflict checks apply to every new inspection route. Responses use `Cache-Control: no-store`.

Support/Admin/Root can request `/profile-preview`. Legal alone is refused. The projection uses the same public name/about, public identity visibility, badges and mode-photo projection as member presentation. It includes mode enabled/visible flags, with public content only for enabled visible modes. Hidden modes have no public preview. Approved public galleries respect existing plan limits and per-mode mains. No member token is minted, no member session is started, no profile visit/search is recorded and no conversation is opened or changed. A staff-preview audit records actor and subject only.

Case links retain Support-vs-Admin queue scoping; conversations are accessed only through existing authorised reports/cases. No unrestricted message or chat-photo endpoint is added. The account workspace retains the existing sanctions, complimentary membership sources/history, staff-badge actions and server-authoritative action forms.

## Restricted photos and action history

`GET /profile-photos` is Admin/Root only. It returns a bounded inventory of profile-photo state, mode assignments and per-mode main assignments. Support/Legal receive no inventory. Hidden/unassigned/pending/rejected images open only through `POST /profile-photos/{photoId}/view`, with an audited purpose, current Q114 photo-review safety acknowledgement and short-lived signed `inspection` link. Images render blurred initially; reveal records another access. Attachments, removed items and specialist-blocked media are excluded at issuance and again by the serving predicate. Rejected/removed objects may already be erased; this feature never restores them or treats a missing image as viewable. Existing case and routine-review audiences retain their own guards.

The grouped account component separates assurance/DOB opening, public mode previews, case links, restricted inventory and action history. New action history is cursor-paged at 25 account-management events per response. Its explicit allow-list excludes Legal activity and all raw audit details, purposes, staff IDs and case evidence. Existing detailed sanctions and case workflows remain authoritative. No broad audit export or all-account listing is introduced.

## Validation record

Domain tests were added for staff-vs-Legal preview separation, hidden-mode filtering, allowed profile-photo states and exclusion of blocked/chat photos. Staff-access checks cover the new capability for every staff role while retaining the existing Admin-denied identity-decryption checks.

Real PostgreSQL regression coverage uses the existing disposable database: truthful numeric age, public mode aliases/about/age visibility, hidden-core/private-field exclusion, pending/hidden-mode photo exclusion, queue-specific case links, no tracked account/search/conversation mutation, and stable 25-item action-history pagination without Legal/raw-detail leakage. Existing account acceptance now includes Support/Admin/Legal/Root API role boundaries, separate current-DOB elevation and Root's continued denial of Legal identity/history routes.

These new executable cases have not yet been run at this record's creation: root integration owns the build/check/browser window. `git diff --check` and changed JavaScript syntax checks pass. No services, installed database records, credentials, screenshots, commits, merges, deployment or Actions settings were changed by this slice. Native/device, assistive-technology and hosted acceptance remain unverified.

Coordinated pre-commit gate `integrated-check-12` passed: check.sh exit0, 690 PostgreSQL cases and all three HTTP smoke stages, zero build warnings/errors. Actual account runs have exercised Support/Admin/Legal/Root inspection and DOB-access boundaries successfully before later unrelated fixture failures; the complete accounts suite is not yet a pass.
