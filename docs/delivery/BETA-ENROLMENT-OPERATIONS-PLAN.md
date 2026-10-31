# Beta enrolment operations slice

D273 DEFAULT under CONFIRMED D62/D76/D91 and the owner's current issue25 queue. Existing stable Admin beta add/list/revoke APIs stay authoritative. Corporate intake/Supabase stay outside this repository.

1. Add the Admin enrolment screen and typed transport, with explicit revoke confirmation, role/antiforgery checks, bounded latest1000 list and fictional screenshots.
2. Record iOS private-distribution status as an explicitly staff-reported fact only: not-recorded/not-invited/invited/accepted/removed, server actor/time, optimistic revision and atomic audit. No Apple API call, invitation, eligibility attestation or authentication bypass. Preserve web beta activation and other gates.
3. Include linked enrolment records in own export and encrypted account evidence; ordinary account deletion already erases them. Retention/email follow-ups stay tracked under25/118 until the invitation dispatch/provider slice; do not call this an email sender or close25 prematurely.
4. Test current roles, invalid/stale/concurrent updates, no-op replay, actual Admin add/activate/revoke/status persistence, narrow UI, essential account rights and audited metadata without addresses. Run relevant suites and ./scripts/check.sh before the slice commit. No deploy/merge/Actions.
