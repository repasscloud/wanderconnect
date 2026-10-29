# Contact retirement policy investigation

CONFIRMED D249 authorizes separate reviewed contact retirement. Mobile retirement is implemented underD254; provider email/login retirement is still being built. This directory contains fictional Java policy checks and an uninstalled, pinned provider guard under development. It is not a configured Keycloak integration. Main realm/hosts are untouched.

A Java21+ JDK can run the checks without Maven or network downloads:

```sh
python3 scripts/check_keycloak_contact_policy.py --java-home /path/to/jdk --keycloak-libs /path/to/public/runtime/libraries
```

Pinned26.8.1 public runtime libraries are required. `--output-jar /new/path/provider.jar` optionally exports public compiled code and refuses to overwrite an existing artifact. Libraries must never contain realm/database exports or credentials. Classes live in private temporary space and are removed afterward. No source/binary is copied into a running provider.

Provider retirement must compare exact observed email/username snapshots; contact normalization cannot hide a concurrently changed binding. The reserved `.invalid` sink identifies only an opaque operation. Any eventual provider-owned guard must protect delayed profile/admin/broker writers, preserve unrelated credentials and prevent recovery through the old mailbox. Uncertain provider effects need durable API reconciliation. Original-account recovery remains separate; a reserved sink must never be called verified ownership.

See the [current plan](../../../docs/superpowers/plans/2026-10-29-banned-identifiers.md). No completed email-retirement, provider-compatibility or UAT-readiness claim follows from these unit checks.


The provider resource is newly implemented and protected by an explicitly enabled realm attribute (`swoovr.contact-policy=enabled`) and its service account (`swoovr.contact-policy.client`, default `swoovr-api-accounts`) with current manage-users permission. It offers capability/raw-binding/status reads and exact email-retirement CAS. Artifact version must beKeycloak26.8.1; PostgreSQL guards install through the provider-owned Liquibase changelog. Internal JPA ABI is unsupported and emits an explicit getEntities removal warning. Do not upgrade without rerunning compatibility tests.

The date protocol is generated from `src/Swoovr.Contracts/ApiContract.cs`. Normal admin profile projections are insufficient for exact binding snapshots. An idempotent completion is confirmed only against current provider state. Minimal linked policy state contains no old contact, fingerprint or copied reason; source deletion cascades it. The unverified reserved sink cannot be written back to a real contact or marked verified through ordinary provider writes. Original-account recovery is separately reviewed; no automatic account transfer.

Run actual isolated compatibility checks with the same JDK/public-libraries environment described in [TESTING.md](../../../docs/delivery/TESTING.md#keycloak-contact-policy-component-d255-not-main-realm-installation). The test creates/removes only its own provider container/database, keeps private logs, and uses fictional identities. It does not install anything into main or establish browser mailbox ownership/external IdP delivery. Durable API pending-state/mail suppression and staff integration are not built yet.
