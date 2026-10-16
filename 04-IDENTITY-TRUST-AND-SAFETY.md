# Identity, authentication and trust

## 1. Authentication

CONFIRMED: Sign in with Apple and Google. DEFAULT: email magic link as independent fallback; passkeys after account creation. Do not add Facebook/X login just because profile social links exist. Provider availability/configuration is a production dependency.

Use standards-based OIDC/OAuth flows via supported SDKs and system browser on mobile, including PKCE and state/nonce validation as applicable. Validate provider issuer, audience, signature and expiry server-side; bind identity to provider subject, not display name. Do not merge accounts merely because email strings match. Linking/unlinking requires reauthentication and an existing authenticated account, with safeguards against removing the last login method. Handle Apple private relay addresses and provider revocation.

Website DEFAULT: backend-for-frontend session with Secure/HttpOnly cookies and CSRF protection; do not require browser localStorage bearer-token persistence. Mobile: secure OS token storage, rotation/revocation, session list and remote logout. Separate admin and venue roles, require stronger authentication for privileged actions. Native app binaries never contain provider client secrets.

## 2. Verification

DEFAULT gate: verify adulthood and identity before live profile browsing/contact; draft profile editing can happen first. Photos remain optional even when a private verification selfie is required. This gate's exact timing and fallback remain OPEN.

Provider evaluates supported document authenticity and selfie/liveness as appropriate. Store minimum outcome/reference, verified age/adulthood and timestamps; do not retain raw identity documents by default. Document provider processing, retention, data residency and exceptions. Legal identity is not public. Verification statuses: not started, pending, verified, needs action, rejected, expired/recheck. Appeal/manual fallback is necessary. No “guaranteed safe” badge; no claim ID eliminates bots.

## 3. Images and account abuse

On upload: validate MIME by decoding, size and dimensions; strip unnecessary metadata; malware/unsafe-content checks; private object storage; SHA-256 of original bytes; hash normalised pixel representation for exact decoded matches; perceptual hash for candidate similarity; compare across accounts and enqueue meaningful matches. Hashes do not prove image ownership or impersonation. Never expose matched account identities to an uploader. Group photos, stock images and legitimate re-created accounts require interpretation. Perceptual hashes can have false positives/negatives; thresholds require validation. Avoid face-recognition identity clustering in the prototype.

Combine with rate limits, repetitive-message/link patterns, device/session risk and user reports. No automated permanent ban solely from image reuse or raw negative feedback. Staff review must include reason, evidence, action and appeal path. Define limited retention for fraud evidence and deletion/tombstone hashes rather than retaining an unlimited hidden library.

## 4. Social accounts

CONFIRMED: optional social media profile links. DEFAULT initial platforms Instagram, Facebook, TikTok, LinkedIn and a personal website; these are proposed options, not promised OAuth integrations. Validate HTTPS URLs against supported domains; no arbitrary executable schemes. Sanitise displayed content; do not fetch untrusted URLs from unrestricted backend networks. User-entered link means “provided by member.” Only label ownership verified after a supported actual verification flow. Do not scrape accounts or import contacts automatically. Social linkage is optional and cannot replace identity verification.

## 5. Privacy and moderation

Adults only. In-product reporting, blocking, content moderation, published support contacts, appeals and documented response ownership are necessary pilot features. Separate private chemistry feedback from reports. Handle urgent threats and suspected minors through appropriate documented escalation processes. No public rating of attractiveness. No coercive sexual solicitation, paid sexual services or exploitation. Casual intent must not be silently removed from requirements, but public content, messaging rules and app-store positioning need explicit policy assessment before launch.

Access exact location only for necessary eligibility; expose coarse areas. Exclude private profiles/media from public indexing. Authorise every media request using revocable access controls; expiring signed URLs have revocation limitations and require short lifetimes or a mediated delivery layer where needed. No claim screenshot prevention is reliable.

Collect consent and purpose for sensitive information; restrict staff access; encrypt transit/storage; audit privileged reads; define deletion/export and retention schedules with professional review for launch markets. Account deletion must revoke access and explain legally necessary retention and subscription management. Do not assume deleting an account automatically cancels an Apple/Google subscription. Avoid sensitive analytics payloads and revealing lock-screen notification previews by default.

## 6. Platform review

As checked 16 October 2026, Apple guidelines include UGC safeguards, restrictions around overt sexual/hookup positioning, login service requirements, account deletion and dating-app differentiation. These are material product constraints, not a guaranteed outcome. Preserve the owner's broad consensual connection vision while evaluating publishable implementations; do not misrepresent the actual product to review teams. Refresh both Apple and Google policies before submission.
