# Disabled Keycloak UAT realm preparation

31 October 2026. **DEFAULT preparation under D31–D39/D221**. Q90 / [issue 124](https://github.com/repasscloud/wanderconnect/issues/124) remains OPEN (latest read: no comments); there is no hosting approval, provider registration or deployed integration. `scripts/uat/keycloak_realms.py` produces deterministic review files, with all three realms **disabled**. It does not contact Keycloak or enable a sign-in path.

## Generate for review

The operator must supply actual nonsecret Entra tenant and application UUIDs, explicit portal origins, and a new absolute output directory outside every Git checkout. No secret is accepted as a CLI argument or read from `.local`/environment. Missing/invalid inputs fail before writing; existing directories are never overwritten. Output directory/files use 0700/0600 permissions.

```sh
cd /Users/danijeljw/Developer/wanderconnect-brief/.claude/worktrees/phase-zero-uat
python3 scripts/uat/keycloak_realms.py \
  --admin-origin https://admin.uat.swoovr.app \
  --venue-origin https://venues.uat.swoovr.app \
  --entra-tenant-id "$SWOOVR_UAT_ENTRA_TENANT_ID" \
  --entra-client-id "$SWOOVR_UAT_ENTRA_CLIENT_ID" \
  --output /absolute/private/operator-directory/new-swoovr-uat-review
```

The two portal hostnames in this example are **DEFAULT proposals**, not provisioned DNS or owner-confirmed origins. Only exact distinct HTTPS subdomains of `uat.swoovr.app` are accepted; ports, wildcard redirects, query strings and paths are refused. The member origin `https://uat.swoovr.app` and passkey RP ID `uat.swoovr.app` follow D39; authentication host `https://auth.uat.swoovr.app` follows the integration design's DEFAULT. Venue callbacks use a proposed separate origin and root callback paths because no hosted venue portal exists; review that choice with the actual future host before enabling it.

Generated files are `swoovr-members.json`, `swoovr-venues.json`, `swoovr-staff.json` and `manifest.json`. The manifest includes artifact hashes, selected origins, broker callback, required runtime environment names and activation gates. No user, credential, signing key, tenant token, export of live state or expanded client secret is included. The current canonical development JSON is read as baseline public configuration; it is not executed or rewritten. A baseline containing users, IdPs or additional component types is refused rather than silently promoted.

## Realm and flow boundaries

Names drop `-dev` per D31. Baseline user-profile fields, Standard Swoovr theme (image-only HTTPS UAT privacy/terms/community links and v0.1.0-uat channel), verified-email requirement, temporary three-failure/15-minute lockout, session limits and code+PKCE client settings are retained. The member, venue and staff web clients have exactly one `/signin-oidc` redirect and one `/signout-callback-oidc` logout redirect on their own selected origins. No password grant, token exchange, implicit flow or sign-in-client service account is enabled. The member-only mobile client retains D93's existing private-use redirect `com.swoovr.app:/oauth2redirect` and `com.swoovr.app:/signedout`; verified universal/app links remain separate.

Members have passkeys enabled at RP `uat.swoovr.app`, with required resident credentials/user verification as in the canonical baseline. A child authentication host may use its parent RP domain, but actual browser origin/passkey behavior still needs isolated acceptance. Explicit custom registration flows include the required 18+ terms action; explicit password/optional-OTP flows carry the same 12-hour AMR configuration as the existing local provisioner. Venue registration stays disabled, with no consumer-social provider or passkey. Apple/Google and email-link extensions are not invented; no configured broker, external account or email-link capability is claimed for members.

Staff's only prepared browser execution is the configured Entra redirector. There is no cookie shortcut, password form, passkey or consumer-social branch. Registration, reset credentials and direct-grant flow bindings point to an explicit deny flow; sign-in clients also refuse direct grants. Its first-broker flow creates only a unique identity; it has no email-based automatic account linking or fallback local-account authentication. The broker alias is `entra`; the API must explicitly receive `Keycloak:StaffSsoAlias=entra` (runtime environment `Keycloak__StaffSsoAlias`) after host configuration is reviewed. Application invitations, verified email, current application roles, session evidence and freshness remain API decisions.

D281 DEFAULT follows the takeover instruction that hosted staff use Entra: the hosted application root grant is assigned by the one-shot operator command to an approved verified staff-realm Entra subject. D36's local password+OTP root path remains Development only; no hosted local-password bypass is introduced. Keycloak's separate temporary master-realm bootstrap operator is not a Swoovr staff account and must be removed after secure identity-server administration is established. No root user/password is included in these artifacts.

## Runtime secrets and Entra operator work

| JSON placeholder | Required Keycloak runtime environment variable | Proposed Key Vault secret name (DEFAULT) |
| --- | --- | --- |
| `${SWOOVR_UAT_MEMBERS_WEB_SECRET}` | `SWOOVR_UAT_MEMBERS_WEB_SECRET` | `swoovr-uat-members-web-secret` |
| `${SWOOVR_UAT_VENUES_WEB_SECRET}` | `SWOOVR_UAT_VENUES_WEB_SECRET` | `swoovr-uat-venues-web-secret` |
| `${SWOOVR_UAT_STAFF_WEB_SECRET}` | `SWOOVR_UAT_STAFF_WEB_SECRET` | `swoovr-uat-staff-web-secret` |
| `${SWOOVR_UAT_ENTRA_CLIENT_SECRET}` | `SWOOVR_UAT_ENTRA_CLIENT_SECRET` | `swoovr-uat-entra-client-secret` |

These are placeholder names, not created secrets/bindings. An approved deployment must grant the intended managed identity read access to the selected Key Vault secrets and explicitly map them into the importing Keycloak process. Matching web-host client secrets must be injected separately. Nothing uploads automatically; do not place resolved exports in the repository or image. Keycloak import resolves environment placeholders. The pinned image imported all three disabled realms using temporary fictional environment secrets; actual provider secrets and approved activation remain operator work.

Tenant/client UUIDs appear as nonsecret literal metadata in the generated broker configuration. The broker uses tenant-specific v2.0 issuer/authorization/token/JWKS URLs, signature validation, no stored broker tokens and no UserInfo call. The tenant must have a reviewed single-tenant Entra app registration; register this broker callback exactly:

```text
https://auth.uat.swoovr.app/realms/swoovr-staff/broker/entra/endpoint
```

No `common`/`organizations` issuer is accepted. A tenant-specific issuer alone does not prove employee status: directory guests may also exist. Entra assignment, approved employee membership, Conditional Access/MFA/device policy and API invitation/subject binding require real operator configuration and negative tests. No employee account type is inferred from its email.

The broker deliberately sets `trustEmail=false`: Microsoft's email claim is mutable and not guaranteed to be correct. Merely receiving `email` must not auto-verify ownership. Verified-email/invitation handling must be proven with the real tenant and the approved email channel; do not change this to unconditional trust just to make sign-in succeed. This may keep staff sign-in blocked, which is an activation gap rather than evidence of a configured integration.

## Activation and evidence gates

All realms stay disabled, SMTP settings are empty and no mail sink is carried over. D281 packages/selects the [MailerSend HTTP email SPI](../architecture/KEYCLOAK-EMAIL.md) in the UAT image. Runtime provider credentials, verified sending domain/DNS and actual acceptance/delivery still require operator validation before registration/verification/recovery activation; no SMTP substitution is proposed. This generator does not configure the API account-management service client, grant any realm-management roles or enable email retirement; those need separately scoped least-privilege review under the contact/backup runbooks.

The exported authentication-flow representation follows the [official Keycloak Admin REST schema](https://www.keycloak.org/docs-api/26.8.1/rest-api/index.html) and [Keycloak authentication/brokering guidance](https://www.keycloak.org/docs/latest/server_admin/index.html). Actual pinned-image checks verify import of all three disabled realms, exact portal callbacks/logout values, no sign-in-client password/service-account grants, member passkey RP metadata, the sole required staff Entra redirector, trustEmail=false and actual Keycloak template invocation of the selected HTTP sender. The sender fails closed on the internal-only network, without exposing temporary secrets in logs. This is metadata/import/provider wiring evidence. First/repeat broker authentication, required-action/browser registration, passkey ceremonies, AMR refresh/freshness and real tenant/guest/email-linking negatives remain unverified; no deployed UAT claim is made.


Microsoft's [tenant-specific OIDC metadata](https://learn.microsoft.com/entra/identity-platform/v2-protocols-oidc) grounds the broker URLs; its [ID token claim reference](https://learn.microsoft.com/entra/identity-platform/id-token-claims-reference#payload-claims) explains the email limitation. These sources do not establish that the operator's tenant, app or users are configured.

Pure checks run without a host/network:

```sh
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tests/uat -p 'test_*.py' -v
```

Actual evidence: 9 Python test methods pass, covering deterministic hashes, exact callbacks, disabled/userless/keyless exports, secrets left as placeholders, explicit terms/passkeys, staff-only Entra flow and no automatic email linking, invalid hosted inputs, private outside-checkout output, no overwrite and symlink/traversal/repository rejection. Temporary synthetic outputs are removed by the tests. The pure checks make no server claim. D281 additionally built/ran the pinned Keycloak image with162 loopback HTTP assertions and41 isolated runtime/import/SPI/private-log/theme-property assertions; no actual Entra/browser/provider-delivery, provisioning, merge, deployment or Actions activation. Run `python3 tests/uat/keycloak_image.py --image swoovr-uat-keycloak:phase-zero-check` for the owned container fixture.
