# Overnight development report, 28–29 November 2026

Updated as work goes. It's the resume point after an interruption. The plan approved by the owner is summarised in [Approved plan](#approved-plan).

## Initial state (28 November 2026, evening)

- `main` = `e2757dc` (v1.3.14). UAT runs v1.3.14 (Deploy UAT succeeded 11:43). Every workflow that is switched on is green.
- Open PRs: **#627** only (Privacy Notice 0.22, D453). It is mergeable, Codex found no issues, and no threads are open.
- Uncommitted: the D454 Photo Verified decision notification on `feature/530-photo-verified-notice`, built on #627.
- 87 open issues. The groups are in the plan. The deferred backlog (#35–#64, #611–#626 compatibility and others) is not touched.
- Owned suites that are stale on main: `review-workflows` (line 79) and `notifications` (line 430).
- Next IDs: **D455**, **Q196**.

## Decision register

The owner approved the plan without answering F1–F7, so every recommended option is used as a **DEFAULT**, and each PR says so. Any of them can still be changed.

| Ref | Question | Applied (DEFAULT) |
|---|---|---|
| F1 | Effective date of Privacy Notice 0.22 (#627) | 28 November 2026 |
| F2 | Photo Verified decision channels (D454) | Notification centre only |
| F3 | #544: how the place is chosen | Current location (one-time) or a pasted maps link or coordinates. Optional label, screened. No map tiles. Card links to Apple Maps, Google Maps and OpenStreetMap |
| F4 | #543/#431: sharing a whole album | Covers the photos in the album at that moment. The sender can stop it. The gate is re-checked on every view |
| F5 | #569 captions | Per photo, 150 characters, moderated like About me |
| F6 | #373 ban-evasion signals | Review only on a verified legal name + date of birth HMAC match with a different email and mobile |
| F7 | Housekeeping closures | #257, #372, #488 (and #509 once checked); #313/#319 checked and closed as the owner asked |

## Approved plan

Waves, in order. A blocked item is recorded and skipped.
1. **Baseline:** this report; D454 PR; fix the stale owned suites; housekeeping (F7).
2. **#544** sharing a location in chat (D455).
3. **#543** chat attachment menu (library, upload, camera), then album photos and albums in chat (#431 §4–5, D456).
4. **#448** attribute model, slice 1 (catalogue, storage, per-mode publication, server-side filtering), then slice 2 (editor and public rendering), **#452**, **#570/#571**, then narrowing **#500**.
5. Independent: **#376** ClamAV freshness. **#569** per-photo report and captions. **#373** if F6 holds.

Every PR that appends to DECISIONS.md is in one linear stack with base `main`, starting with #627.

## Progress log

| Time | Item | Result |
|---|---|---|
| 28 Nov, night | Plan approved | Report started |
| | Housekeeping (F7) | Closed #319, #509, #372, #488 and #257 with evidence comments. #313: the repository still names `hello@repasscloud.com`; I can't read the UAT host, so I asked on the issue |
| | PR A (D454) | Committed and pushed. **#628** opened, Codex review requested. check.sh passed. The owned suite `photo-verified` passed with the new notification checks |
| | Stale suites | **#630**: `notifications` (held the Connections page's own centre read; PASS 147) and `review-workflows` (missing staff OTP; PASS). Test-only |
| | #544 (D455) | **#631**. check.sh passed; `chat-location` PASS (36). Screenshot checked. Codex requested |
| | #543a (D456) | **#632**. check.sh passed (on the stack); `chat-library` PASS (20). Codex requested |
| | #448 slice 1 (D457) | **#633**. Catalogue, `profile_attribute_values` (migration `ProfileAttributes`), API, per-mode details, snapshot, export, deletion. check.sh passed; `profile-details` PASS (21) |
| | #448 slice 2 (D458) | **#634**. More about you on /me/profile; More about me on profiles. `profile-details` PASS (31); editor screenshot checked |
| | Codex on #631–#633 | #631: location is a reply in the client (fixed 0239d01, suite covers it live). Text-harm history now counts labelled locations (fix + runtime check, check.sh running). #632/#633 repeated the first finding: replied and resolved |
| | #452 (D459) | "You both like…" when both members publish interests in the mode. check.sh and suite running |
| | #571 (D460) | Their trip to your area on the profile, by the Who's visiting rule. General availability is already a detail (D457). Suite pending |

## Pull requests (merge order)

| Order | PR | Issues | Summary | Tests | Codex | Ready |
|---|---|---|---|---|---|---|
| 1 | #627 | #609, #530, #544 | Privacy Notice 0.22 | check.sh, photo-verified suite | no findings | yes |
| 2 | #628 | #530 | Photo Verified decision notification (D454) | check.sh, photo-verified suite | no findings | yes |
| 3 | #631 | #544 | Share a location in chat (D455) | check.sh, chat-location (36) | requested | yes |
| 4 | #632 | #543 (part) | Chat + menu: library, upload, camera (D456) | check.sh, chat-library (20) | requested | yes |
| 5 | #633 | #448 | Profile details slice 1 (D457, migration `ProfileAttributes`) | check.sh, profile-details (21) | finding fixed upstream | yes |
| 6 | #634 | #448 | Profile details slice 2: web (D458) | profile-details (31) | requested | yes |
| any | #630 | — | Fix stale owned suites (test-only) | notifications (147), review-workflows | — | yes |
| any | #629 | — | This report (docs) | — | — | yes |

## Decisions made independently

- **D455 storage:** a shared location is stored as the message's own text (`geo:lat,lon` plus a label line), so every text rule (unsend, 30-day evidence, purge, holds, report, removal, export, deletion) covers it. No migration.
- **D455:** 0,0 is refused, and points are kept to 5 decimal places.
- **D457:** nationality, causes and accessibility count as sensitive (unavailable). Free-text details wait for a slice with text screening. Option lists are drafted.
- **D459:** publishing your own interests in a mode is opting in to "You both like…" there.
- **D460:** only a trip to the viewer's own area shows on a profile, by the Who's visiting rule.
- **D456:** a library photo sent to a chat is re-run through the upload pipeline as a fresh chat copy, rather than reusing the library item. Retention, removal and the explicit gates then never couple the two.

## Blockers and owner actions

- After #627 merges and is deployed, an operator stages and publishes the 0.22 policy-update notice. I'm not doing this.

## Remaining work

To be filled in as the run progresses.
