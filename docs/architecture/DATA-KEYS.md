# Data key compatibility and rotation

DEFAULT D234 implements the existing versioned-key boundaries (D187/D189/D191/D228). Founder coupon issuance still uses the highest configured founder version. Legacy data keys labelled1, and unversioned lookup/signing keys, derive only from founder secret1 with their original purpose labels. Adding founder secret2 must not change what encryption1 means or silently invalidate existing fingerprints, phone challenges, device tokens or media links.

| Feature | Explicit configuration | Legacy fallback |
| --- | --- | --- |
| Evidence and sealed police references | `Evidence:Keys:<byte version>`; base64,32 bytes | founder1 + `swoovr:evidence:encryption:v1` |
| Contact history encryption | `ContactHistory:Keys:<byte version>`; base64,32 bytes | founder1 + `swoovr:contact-history:encryption:v1` |
| Contact lookup | `ContactHistory:LookupKey`; base64 | founder1 + `swoovr:contact-history:lookup:v1` |
| Minimum identity store encryption | `IdentityStore:Keys:<byte version>`; base64,32 bytes | founder1 + `swoovr:identity-store:encryption:v1` |
| Identity match | `IdentityStore:HmacKey`; base64 | founder1 + `swoovr:identity-store:match:v1` |
| Phone-code/IP hash | `Phone:CodeKey`; UTF-8 secret | founder1 + `swoovr:phone-code:v1` |
| Existing terminated email/mobile holds | `Accounts:HoldKey`; UTF-8 secret | founder1 + `swoovr:terminated-email-hold:v1` (mobile has an additional typed `tel:` prefix) |
| Media links | `Media:LinkKey`; base64,32 bytes | founder1 + `swoovr:media-links:v1` |
| Push device encryption | `Push:TokenKeys:<positive int version>`; base64,32 bytes | Development only: founder1 + `swoovr:push-token:encryption` |
| Push lookup | `Push:TokenLookupKey`; base64,32 bytes | Development only: founder1 + `swoovr:push-token:lookup` |

Explicit values keep precedence. If founder1 is absent, no legacy fallback is derived from another version. Adding an explicit newer encryption key preserves derived1 while founder1 remains configured; this also applies to Development push. Hosted push continues to require explicit keys. The full hosted configuration/startup enforcement is part of UAT readiness, not a completed deployment claim. The approved persistent banned-identifier registry is separate remaining work and must use its own versioned matching keys.

The algorithm and existing bytes are unchanged: purpose-separated [HMAC-SHA256](https://learn.microsoft.com/dotnet/api/system.security.cryptography.hmacsha256.hashdata?view=net-10.0). All examples in automated checks use fictional generated or fixed fixture keys; production secrets belong in the managed key store and must never be copied into files or logs.

## Operator procedure

1. Inventory versions with `keys status`. This reports version counts, not proof that the configured bytes decrypt each row. Keep all still-required keys, including those needed by retained evidence and backups.
2. For a source using derived1, retain founder1 or configure the exact existing feature key explicitly as version1. Configure a separate newer feature encryption version; rotating founder coupons alone is not data-key rotation.
3. Run `keys rotate contact-history|evidence|police|identity --to <version>` with both source and destination keys available. Check results and actual authorised readback. Do not remove source keys merely because the live rows migrated: backup/restore and retained sealed objects may still require them. Existing `keys rotate` does not migrate raw sealed media objects or protected push tokens; retain those keys until a separately verified migration/expiry permits retirement.
4. Unversioned lookup keys cannot simply change without a reviewed matching-data migration. In particular, existing irreversible terminated-contact fingerprints cannot be re-keyed without a trustworthy source. The new registry will support versioned matching explicitly. Coordinate phone/link key replacement with outstanding short-lived challenges/links.

## Historical mislabelling

Older code derived encryption1 from the *current* founder secret. If another environment actually wrote version1 data while founder2 was current, its historical version1 bytes came from founder2 and the original purpose label. Pinning future derivation to1 does not repair those records automatically.

Stop and establish the actual source from secure deployment/configuration history. Preserve a restricted backup and every candidate source key. A known single historical source can be supplied explicitly under the existing feature key version for controlled readback/migration. Mixed sources sharing the same envelope version require a reviewed, provenance-aware recovery procedure; do not guess, bulk overwrite, print plaintext, or repeatedly rotate with arbitrary keys. Cryptographic failures leave the affected records retained, not evidence that they can be erased. No historical production recovery is claimed.

For the local takeover run, only the name `SWOOVR_FOUNDER_SECRET_V1` was present; secret values were not printed. The regression instead used fictional founder1/founder2 values and reproduced authenticated-decryption failure before the fix.
