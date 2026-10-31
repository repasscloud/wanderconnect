# Meeting safety implementation boundary

CONFIRMED D218/D219 (Q88/Q89, issues [122](https://github.com/repasscloud/wanderconnect/issues/122), [123](https://github.com/repasscloud/wanderconnect/issues/123), parent [40](https://github.com/repasscloud/wanderconnect/issues/40)) define the approved scope. DEFAULT D263 supplies the bounded implementation choices below. This is a local prototype slice; final integration evidence is pending the coordinating run. No hosted service, real recipient delivery, physical-device acceptance or legal approval57 is established by this document.

## Member workflow and disclosure

Meeting safety is voluntary and free for every member. The standalone “I'm meeting someone” form does not depend on Plan a Date, mutual QR, another Swoovr participant, commerce or reputation. A member can save up to three email/E.164 mobile contacts and select confirmed contacts per session. Each must opt in through an expiring, single-use invitation; DEFAULT invitation lifetime is seven days. Decline, withdrawal, unsubscribe, removal and changed destination prevent further authorised sharing; changed details require fresh consent. Contacts are never marketing recipients.

Store UTC instants and the selected meeting time-zone ID; display the meeting zone and offset rather than the visitor's local zone. Member-provided first name, start/end, optional public venue/general area, note and optional check-in times remain distinct from independently verified information. The external summary never includes another participant's identity, profile, pictures, messages, private address or live location. DEFAULT incoming external notifications contain only the preferred first name, generic status and resource/consent links; no time or place is exposed before contact code verification.

Member actions are I'm OK, Extend by 30 minutes, I'm home safe, End meeting, Cancel meeting and Need help. Server-side scheduling handles prompts and the 30-minute grace independently of app/push availability, including missed/unconfirmed escalation to selected confirmed contacts only. A missed check-in is not proof of an emergency. Home safe asserts only what the member selected; End meeting makes no safety assertion. Contacts cannot perform these actions. Need help alerts contacts and shows country-specific support; it never automatically contacts police or dispatches responders.

The separate emergency action opens the device dialler with the selected country's verified resource number; it never automatically calls. Alert trusted contact opens the device SMS composer. An optional maps link is explicitly chosen and generated on the device; exact coordinates never reach Swoovr. Composer opening is not evidence of sending or delivery. Optional after-meeting feedback, report and dismiss remain separate choices. Feedback is available only in the author's member features, does not automatically change reputation, and remains subject to lawful restricted access/preservation. A submitted incident links meeting context to the existing serious-incident process without automatically notifying the accused.

## External resource verification — DEFAULT D263

Each meeting/contact gets independent random opaque links with at least 256 bits of entropy; only digests persist. Consent precedes detail access. A link alone does not reveal meeting details: send a six-digit code to the confirmed destination, valid for five minutes, single-use, superseded by a replacement and refused after five failed attempts. Store a keyed code hash, not plaintext. Apply a 60-second recipient cooldown and destination-wide limits of three code requests/hour and ten/day across members and replicas.

Successful verification creates a random read-only grant, persisted only as a digest, valid for at most four hours and no later than the link expiry. The SSR host holds it in a link-bound Data Protection-protected HttpOnly cookie, Secure outside Development, SameSite=Lax, path /safety. Every read rechecks current consent, member/account availability, recipient/link revocation and both expiries. Missing or invalid verification returns the code gate or a neutral unavailable page without sensitive details. This verifies access to one external resource; it creates no member account, member authentication session, role or staff access. Keycloak remains the member authentication backend.

Public token pages use no-store, no-referrer, noindex/nofollow/noarchive, restrictive CSP, no third-party tracking/fonts/scripts and no sensitive social metadata. Tokens must be redacted from application, proxy, Cloudflare, exception and support logs; local middleware is not proof that hosted logging is configured. Tokens are not meeting IDs or hashes of destinations; meeting details never enter URL query parameters.

## Scheduling, delivery and abuse bounds

The existing API feature services own durable scheduling/outbox work, with idempotent event keys and recorded attempt/outcome state; no additional worker project. Save intent before remote effects. An interrupted or uncertain acceptance is recorded as unknown and must not be blindly resent. Suppress obsolete prompts and sends after cancellation, closure, account removal or consent withdrawal. Show failed, unknown, simulated and rate-limited outcomes honestly; provider or local-sink acknowledgement never proves recipient arrival or reading.

DEFAULT D263 sender budgets: ten sends/hour and forty/day per contact; twenty/hour and one hundred/day per destination across contacts/members. Budget exhaustion is visible as rate-limited, never delivered. These send budgets are separate from invitation limits and the stricter code-request quotas. Mailpit is local-only and SMS remains simulated under D183 until separately configured/accepted. Real provider availability, quota operation and external acceptance require their own evidence.

DEFAULT after-meeting prompt timing is final scheduled check-in/end plus two hours. Unresolved help/missed status, cancellation and account revocation suppress the prompt; the member may still explicitly report an incident from the session. Feedback and dismissal do not remove that reporting route.

## Four independent lifecycle clocks

| Concept | Boundary |
| --- | --- |
| Shared link | Earlier of final expected/check-in end+24 hours and its original creation+30 days. Extensions may adjust still-valid links only within their original cap; expired/revoked tokens never resume. |
| Session closure | Home safe, end or cancel closes promptly; unattended sessions auto-expire at final end+24 hours. Completed final-status pages remain usable up to 24 hours after closure, subject to earlier link expiry/revocation. |
| Ordinary records | Purge ordinary session records 30 days after final closure; retained delivery/event metadata is minimal. Withdrawn/expired contact data follows its separate purge clock and references. |
| Preservation | Active/overlapping holds and linked abuse/incident review obligations postpone ordinary disposal. Unknown preservation authority fails closed. Closure and link expiry do not release a hold. |

Account deletion/contact withdrawal revoke sharing promptly; retention may still preserve restricted records lawfully. Incident-linked source sessions remain protected for their separate review lifecycle. Do not claim that an expired public link means every underlying record was erased.

## Implementation and acceptance

Code boundaries: Domain meeting/contact policies; Infrastructure entities/migrations; Contracts and typed Client; API Safety store, schedule, delivery, verification and retention; Web SSR external resource pages and Web.Client member controls; MAUI member controls. Existing Keycloak, notification, support, incident and preservation boundaries continue to apply. See [architecture](../architecture/README.md), [authentication](../architecture/AUTHENTICATION.md) and the shared DRAFT privacy notice in Swoovr.Client.

Required final evidence covers actual PostgreSQL/API/SSR member and external flows, token uniqueness/hash storage, code expiry/failure/replay/cooldown/destination limits, grant/link expiry/revocation, absence of pre-verification details, sender budgets, consent withdrawal, missed/late check-ins/restarts/duplicate scheduling, time zones/DST, delivery failure/uncertainty, cancellation, account revocation and legal-hold/report retention. Verify web mobile widths/accessibility and affected regressions, native builds and repository gate. This document records no new pass counts: final-run results and limitations belong in VALIDATION.md. Physical-device accessibility/composer/call behaviour, real external delivery, hosted token-log redaction, legal57 and UAT provisioning remain separate acceptance gates.
