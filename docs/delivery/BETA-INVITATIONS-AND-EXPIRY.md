# Beta invitations and source expiry

D275 DEFAULT completes application invitation preparation under CONFIRMED D62/D76/D91. The existing corporate intake/Supabase queue stays in the separate swoovr.com project. External private distribution is still performed by the owner and merely recorded as a staff report in Swoovr. Email acceptance never activates an account or verifies Apple eligibility.

## Configuration and workflow

`Beta__Invitations__Enabled=false` is the default. When explicitly enabled, `Beta__Invitations__PublicBaseUrl` must be an absolute origin with no credentials/path/query/fragment, HTTPS outside Development (loopback HTTP accepted only locally). Set the UAT value to `https://uat.swoovr.app/`. API and the `beta add` command atomically set one pending invitation on a newly inserted enrolment. Duplicate live-address adds never queue a second message; historical rows migrate to not-queued and enabling the feature does not backfill them. The separate email provider must be configured; hosted readiness must require actual MailerSend for enabled invitations.

The API worker queries due rows every15 seconds, at most100 per query, using database state across restarts/replicas. Each row has its own bounded preservation/row-lock scope. A claim and incremented attempt commit before external work. Recheck revocation, activation and72-hour expiry under a row lock immediately before dispatch. An in-flight bounded send serializes with revocation; an already-started effect may complete before revocation commits. Revocation completed before dispatch cancels it. After a process interruption, an old dispatching claim becomes unknown rather than being resent. Provider acceptance and recipient delivery remain separate. No provider error text or destination/body is logged in this workflow.

Known no-effect retry outcomes schedule another attempt after5 minutes, at most3 attempts within72 hours. Rejected, unknown and accepted are terminal. There is no automatic retry of a possibly effective request. Pending invitation expiry is separate from the90-day enrolment clock; an expired email queue does not itself revoke account access. Admin displays queue/outcome/attempt metadata and keeps email status separate from beta activation and external iOS records. No delivery/read webhook claim is made.

## Privacy and retention

No second email/body copy is stored for the outbox; delivery metadata is on the enrolment. Linked own export includes that metadata and separately encrypted account preservation serializes it with the source. Ordinary final account deletion erases linked source rows under existing preservation safeguards.

The scheduled retention job expires unactivated unlinked rows90 days after creation and revoked rows90 days after revocation. A recent revocation uses its own clock even when creation is older. Active linked access remains until account deletion. The preservation barrier serializes expiry with activation, dispatch and capture; unavailable/unknown provenance stops expiry. Held accounts and retained encrypted evidence defer linked expiry. An unlinked email cannot safely establish historical account identity, so any unresolved held/copy population conservatively defers unlinked expiry. This is a proposed minimisation DEFAULT for legal57 review, not a promise that evidence disappears at90 days.

## Verification and operator boundary

Use the owned beta invitation browser suite with invitations enabled and the local public origin; only Mailpit receives fictional addresses. PostgreSQL tests cover opt-out defaults, atomic/idempotent queue, cancellation, accepted/retry/unknown/interruption, multiple workers, a positively observed row-lock/revocation race, independent expiry, unknown hold status, active holds and retained copies. Exact results, initial failures/fixes, screenshots and native compilation are in VALIDATION.md.

Owner/operator work remains: verify sender/domain/DNS, supply the scoped MailerSend token via Key Vault, configure Keycloak email, perform external Apple distribution, and verify actual hosted acceptance/delivery after deployment approval. These local tests do not establish any of those external actions. No deployment, merge or Actions enablement is part of this slice.
