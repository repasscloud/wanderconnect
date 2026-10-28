# Push notifications

CONFIRMED D210/Q83; implementation DEFAULTs D226–D228. `IPushSender` remains in Infrastructure; notification eligibility stays in Api. No additional application project. APNs and FCM are configured independently and never replace Keycloak authentication.

## Configuration

The configuration names below use .NET colon notation; environment variables replace every colon with `__`. Provision separate values/secrets for Development, UAT and Production. Nothing uploads automatically. Hosted secrets belong in the environment's Key Vault, supplied as runtime secret references. Never place private keys in appsettings, images, source, PRs or logs.

| Name | Type / source | Meaning |
| --- | --- | --- |
| Push:Provider | Runtime variable | disabled, simulated, providers. Missing: simulated only in Development, disabled elsewhere. Explicit simulated outside Development fails startup. |
| Push:Apns:Enabled | Boolean runtime variable | Enable iOS provider in providers mode. |
| Push:Apns:KeyId / TeamId / BundleId | Runtime variables | Apple identifiers; bundle defaults com.swoovr.app. |
| Push:Apns:PrivateKeyPem | Runtime secret | Actual PEM text from the .p8 key, including line breaks. |
| Push:Apns:Sandbox | Boolean runtime variable | Explicit APNs sandbox choice; distribution/TestFlight uses the production APNs environment. Match app entitlement/profile. |
| Push:Fcm:Enabled | Boolean runtime variable | Enable Android provider in providers mode. |
| Push:Fcm:ProjectId / ClientEmail / PrivateKeyId | Runtime variables | Selected service-account/project identifiers; separate projects/accounts per environment. |
| Push:Fcm:PrivateKeyPem | Runtime secret | Service-account RSA PEM text; no service-account file is committed. |
| Push:TokenKeys:1 (and later versions) | Runtime secret, base64 32 bytes | AES-GCM token encryption ring; highest configured integer version writes. Retain old keys while referenced. |
| Push:TokenLookupKey | Runtime secret, base64 32 bytes | Stable HMAC lookup key, independently managed. Changing it requires a planned re-registration/migration. |

An enabled provider with missing/invalid credentials, unknown mode, or providers mode with neither platform enabled fails startup. Disabled platforms send nothing. Invalid provider configuration never selects simulation. Provider mode also requires token-protection keys. Development alone can derive local token-protection keys from its existing founder secret. The ignored local configuration/helper does not automatically forward new Push variables: export them explicitly when starting the host; back up shared local files before editing them.

## Device lifecycle and privacy

The stable `POST /api/me/devices` date contract adds optional InstallationId (random UUID). Native provider builds send it with a real token. Existing date-contract clients may omit it and retain token-based registration identity; rotation in place uses the new optional field. Provider mode rejects explicitly simulated tokens. Owned installations rotate in place; possession of an identical provider token transfers it away from a previous account without revealing that account. Arbitrary other installations cannot be rotated. Capacity applies to creation and reactivation. DELETE is owned and available even to a barred member.

Token ciphertext is bound to device/account/platform; the old token column stores a platform-separated HMAC. API views/audits never expose tokens. Provider HTTP clients remove request logging (APNs embeds its token in its URL) and refuse redirects. Only ids/status codes enter notification logs. Provider error bodies stay in memory and are not returned/logged. Wrong or missing decrypt keys fail closed; restore the ring or re-register, never fall back to a plaintext value.

`PushDeviceProtection` only adds nullable columns/indexes. Development startup upgrades legacy simulated registrations; hosted operators run `push tokens protect` after migrations and before serving. It prints a count only. Existing old-main local binaries still understand the schema; they cannot produce real provider registrations. A downgrade must disable push; old binaries must never interpret HMACs as real tokens.

## Outcomes and retries

A provider response records `accepted`, not delivered/displayed/read. Simulation is `simulated` and Mailpit-only. Network/cancellation uncertainty is `unknown` with no blind resend. The sweep also marks interrupted durable intents older than five minutes unknown; it never uses them as resend candidates. APNs invalid-device responses and FCM's explicit UNREGISTERED invalidate only the matching stored token; FCM INVALID_ARGUMENT does not, because it can refer to the payload. A stale rejection cannot invalidate a rotated token.

Each device attempt has durable intent/outcome and an attempt id. Explicit 429/500/503 responses schedule at most three attempts; Retry-After is respected with a one-minute minimum. Delays over one day become deferred for operator review, never an early retry. The existing API worker/`notifications sweep` processes due attempts; its 15-minute cadence can delay retry. Every retry rechecks current account/block/sanction/preferences/mute/quiet-hours/privacy. Retries omit stored private message content and never repeat email. Changed eligibility suppresses pending retry. Quiet-hour suppression is not a promise to deliver later.

Account/security notices retain their independent centre/email/incident channels; push must not be the only security path. No acceptance or retry status establishes that a person saw a message.

## Native builds and acceptance gates

Default Debug remains labelled simulation; default Release never manufactures a token. EnableApnsPush opts into iOS permission request, APNs callback, SecureStorage token, registration/rotation and guarded notification navigation. Set ApnsEnvironment to development or production to match the signing profile. EnableFcmPush opts into the Android binding/service/permission/channel; supply an external SwoovrGoogleServicesJson file for Release. The 124.1.2 binding implements the owner-approved token/HTTP-v1 contract; upgrading to Firebase's newer installation-registration API is a separate compatibility change. No analytics SDK is intentionally enabled by this feature.

The member requests OS permission using Enable notifications on this device. Sign-in retries saved registration; callbacks rotate it. Logout attempts owned API removal before forgetting the session, clears local token/id, and attempts OS/provider unregistration. Offline removal is best effort, not a guarantee; provider invalidation and later token ownership transfer also apply. Cold-start taps wait for the shell/sign-in; only known member destinations open and ordinary API authorization remains in force. Distinct Android intent identities preserve each notification's destination.

Owner gates: APNs key/team/app entitlement/profile, Firebase project/service account/app configuration, and physical iOS/Android tests for permission denial, rotation, foreground/background/terminated state, logout, invalidation, quiet hours, block and deep links. Unsigned simulator compilation and fake HTTP tests do not satisfy physical-device delivery acceptance. No credentials or deployment is supplied/authorised by this document.

## Primary references

[Apple request protocol](https://developer.apple.com/documentation/usernotifications/sending-notification-requests-to-apns), [Apple response rules](https://developer.apple.com/documentation/usernotifications/handling-notification-responses-from-apns), [Firebase HTTP v1](https://firebase.google.com/docs/cloud-messaging/send/v1-api), [Firebase errors](https://firebase.google.com/docs/cloud-messaging/error-codes), [Google service-account OAuth](https://developers.google.com/identity/protocols/oauth2/service-account), [Microsoft MAUI native hooks](https://learn.microsoft.com/dotnet/maui/data-cloud/push-notifications?view=net-maui-10.0), [token-compatible Android binding](https://www.nuget.org/packages/Xamarin.Firebase.Messaging/124.1.2).

Physical credentials/device acceptance is tracked separately in [issue127](https://github.com/repasscloud/wanderconnect/issues/127) (Q83 answered; operational work remains). Local Android simulation screenshot: [settings](../delivery/evidence/2026-10-29-push/android-notifications-simulated.png).
