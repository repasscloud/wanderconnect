# Internal beta scorecard

D272 DEFAULT implements the bounded Phase0 issue51 under the owner's current queue, superseding the older D179 deferral for this scope. It supports internal product/safety investigation with about100 testers; it does not establish statistically reliable outcomes, member quality or verified compliance.

## Contract and collection

`GET /api/admin/beta-scorecard?weeksAgo=1` uses the existing Swoovr-Version contract header and current Admin/root authorization.1–12 select fixed completed UTC Monday-to-Monday weeks. No other filter or duplicate selector is accepted. Member, support and anonymous callers cannot read it. API and `/beta-scorecard` SSR responses are no-store. Reads write a minimal existing audit event with the selected week, never counts, member IDs or query payloads.

`BetaAnalytics:Enabled=false` by default. When enabled, successful authorized member API access can upsert one `(AccountId,UTCday,Dataset)` observation. Barred members, staff and bots are excluded. Successful eligible Explore also sets monotonic success/has-peer flags. No client beacon/SDK, IP, device, filter, mode, intention, location, counterpart or body is collected. An independent database context and1-second deadline keep analytics failure from poisoning a product transaction. Collection is best-effort; gaps remain unknown. The insert locks and rechecks the current account row so a delayed GET cannot recreate observations after deletion authority has cleared them.

Activity includes background authenticated API requests. It is not evidence that someone read a screen, was online to other members or wanted contact. Reporting dataset labels reuse the existing explicitly selected reporting policy; UAT/synthetic data is never claimed as production evidence.

## Measures and limits

- Registrations: retained non-bot member-realm local account records created that week, including uninvited registrations. Not Keycloak form starts or an enrolled-only cohort.
- Profile readiness: current retained cohort snapshot with DOB/name/home city/enabled mode, not verified identity or historical completion.
- Activation: retained signup cohort observed successfully completing eligible Explore in its signup week. Discovery quality: observed successful Explore actors with an eligible peer / observed Explore actors. Neither establishes a view, contact attempt, availability or reply.
- D7/D30: retained signup cohort with an observed authenticated API request on its exact UTC signup day+7/+30 / retained cohort due in the selected week. Observed lower bounds with incomplete collection, not measured human retention.
- Acceptance: retained requests sent in the week accepted by its end / retained requests sent. Reciprocity uses text/photo sender metadata from both sides after acceptance and before a known end. Lazy conversation creation is supported. Unknown ended intervals are excluded; shared cross-mode conversations are disclosed. No message body, delivery, satisfaction or safety finding is derived.
- Safety: retained report counts, accounts reported in multiple distinct cases, and appeal outcomes decided in the week. Allegations are never counted as confirmed offending. Complete first-action time, real-world attendance/quality and commercial results remain explicitly not-recorded; private feedback is excluded.

Every denominator and definition travels with the result. All numeric cells are suppressed together if any positive count/denominator or complementary ratio cell is1–4. Zero denominators have no percentage. No individual rows or trait/location/mode breakdowns are exposed. Sources may be deleted/corrected; this is an internal dated query over retained sources, not an immutable publication or reconstructed population total.

## Lifecycle and evidence

Keep the current UTC day and previous89 days (90 UTC dates) by DEFAULT. Ordinary daily records appear only in their owner's export and erase at final account deletion. Account holds capture encrypted beta-journey evidence; held records/copies and unknown provenance defer scheduled expiry. Copies retain their independent safety schedule. The coordinated retention job handles expiry even when collection is later disabled. Privacy notice Draft0.12 describes the purpose/limits; legal57 remains open, and enabling collection is not legal approval.

Actual checks/results and fictional Admin screenshots are recorded in VALIDATION.md. Configured external analytics, human outcomes and population completeness are not claimed. No deployment, merge or Actions activation.
