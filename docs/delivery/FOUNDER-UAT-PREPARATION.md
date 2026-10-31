# Founder UAT preparation

D276 DEFAULT addresses issue26's capacity evidence and managed-secret operator preparation under CONFIRMED D63/D65/D130 and D90's existing subscription deferral. No billing integration, provisioning or deployment occurs here.

## Application state

Automatic eligibility/allocation after required profile setup and ID and/or Photo verification was built in D155. Deletion-request resend was built with the account deletion workflow. D274 provides the explicitly selectable MailerSend HTTP application sender. These remain distinct from a real hosted provider acceptance/delivery check. The private daily `founders allocate` command remains the scheduler entry point; UAT infrastructure must supply its one-shot schedule and secrets.

The new actual API capacity test starts an owned database/API with `Founders__Places=1`. With explicitly synthetic verified-eligibility SQL fixtures, two concurrent grant requests produce one201 and one409 founder-places-full; exactly one place/profile/token persists. A third request at the configured current count refuses, and a repeated grant cannot duplicate it. Only the winner gets local Mailpit email. Resending keeps the original founder timing and token row. This establishes database/API capacity enforcement, not verification-provider evidence, real delivery or paid benefits. The production maximum remains1,000.

D90 explicitly places the offers/eligibility/entitlements model and channel-specific discounted purchase offers with subscriptions. Do not turn this Phase0 preparation into billing. The three-month Plus/then50%-discount requirements remain preserved for that model and must never restart on restoration; this slice does not claim those unbuilt commercial entitlements are live.

## Managed key contract for UAT

- Runtime environment `Founders__Secrets__1` references a Container Apps secret named `founder-key-v1`, backed by Key Vault secret `swoovr-uat-founder-key-v1`. Do not put a value in environment templates, files, images, argv or this repository.
- A founder key is at least32 random bytes encoded as64 hexadecimal characters. The highest logical version issues new coupons; retain every required historical version for verification, encrypted evidence and backups. `keys status` prints version counts only.
- Pin cryptographic material to a specific Key Vault secret version URI (`https://<vault>.vault.azure.net/secrets/swoovr-uat-founder-key-v1/<vault-version>`). Never silently change the bytes under logical application version1. Founder/data-key logical versions and Key Vault's physical versions are different concepts.
- Rotation adds a separately generated `swoovr-uat-founder-key-v2`/`Founders__Secrets__2`, preserves1 and references both from each API/operator job revision. This does not re-key data derived from1. Use [DATA-KEYS.md](../architecture/DATA-KEYS.md) for purpose-separated explicit data keys and existing migration limitations.
- UAT must configure explicit feature data keys instead of relying on Development fallback derivation. Restore/held-data obligations can require old keys after a live-row rotation. Runtime managed identities receive only the approved Key Vault read role; the owner/key custodian handles creation/rotation.

Illustrative owner-only creation after the vault/access are provisioned, only when this logical name has never held material (nothing below has been executed):

```sh
python3 -c 'import secrets,sys; sys.stdout.write(secrets.token_hex(32))' |
  az keyvault secret set --vault-name "$SWOOVR_UAT_VAULT" \
    --name swoovr-uat-founder-key-v1 --file /dev/stdin --encoding utf-8 \
    --query id --output tsv
```

The pipeline keeps the value out of files/argv and prints only its versioned secret URI. Store the URI as deployment metadata, then bind it through the Container Apps managed identity secret reference. Do not run creation again to replace a logical version. Hosted infrastructure/startup enforcement, actual managed-identity readback, daily job acceptance and restore verification remain in the original UAT readiness slice; this document alone does not implement them. Issue26 stays open until that preparation is included in the draft PR. Provider/domain/credentials remain owner/operator gates.

The [Azure CLI secret options](https://learn.microsoft.com/cli/azure/keyvault/secret?view=azure-cli-latest) and [Container Apps managed-identity/versioned secret references](https://learn.microsoft.com/azure/container-apps/manage-secrets#reference-secret-from-key-vault) were checked31 October2026. Versionless references automatically refresh and restart referencing revisions; cryptographic logical versions therefore use pinned physical secret URIs.

D286 aligns secret reference names with the canonical UAT manifest (founder-key-v1). The logical Founders__Secrets__1 contract and retained historical material are unchanged. The services job now provides the daily founder schedule, off until operator acceptance.
