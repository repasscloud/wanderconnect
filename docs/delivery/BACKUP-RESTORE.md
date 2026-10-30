# Encrypted backups and quarantined recovery

D261 DEFAULT implements D216 and the reversible D221 Azure hosting default. This is deployment preparation and locally tested recovery tooling. No Azure resource, scheduled hosted job, live R2 recovery or disaster recovery objective has been verified.

## Capture and expiry

`scripts/backups/backup.py` streams a PostgreSQL custom archive into age X25519 encryption. API capture must use a trusted executable wrapper invoking `dotnet <published-Swoovr.Api.dll> backups bundle`; a database-only dump is refused for complete API backups. The bundle includes every referenced private media object and preserved source, checked against database hashes, with the database and media list from one exported PostgreSQL snapshot. Keycloak uses its separate PostgreSQL custom archive. Existing media encryption keys must remain available for the lifetime of their backups.

Daily generations expire after30 days, weekly after28 days, monthly after12 calendar months. `scheduled.py` seeds all three tiers and independently checks UTC day/week/month commit markers on retries. Deployment must configure a daily trigger, parallelism1 per role and monitoring of failed/missing generations. Capture does not extend old generation clocks. `azure_store.py` publishes ciphertext before its metadata commit marker; expiry validates the catalogue before deleting eligible archives. The Azure lifecycle policy is a delayed service backstop; the exact calendar schedule is enforced by the expiry job.

`infra/uat/modules/backup-storage.bicep` prepares isolated private Blob storage with shared-key/public access disabled. Publisher, expirer and recovery identities have separate minimum data permissions; ordinary application identities receive none. Private endpoint/DNS topology and scheduled job wiring belong to the parent UAT infrastructure. No Actions workflow is enabled. Managed identity is explicit: no development credential or simulated fallback. Recovery keys belong in Key Vault references, never repository files. Hosted age decryption accepts `SWOOVR_AGE_IDENTITY` through stdin; local operators may use an external private0600 identity file.

## Operator recovery sequence

Run the following only in an isolated recovery environment with explicit PGHOST/PGPORT/PGUSER/PGPASSWORD/PGSSLMODE configuration, matching application/provider versions and all required data keys. Keep ingress disabled. Store archives outside every Git checkout on private ephemeral storage. Commands below use placeholders, not credentials. `STORE`, generation ids, target names and paths must be supplied from the restricted operator inventory.

1. Download and validate the required encrypted API and Keycloak generations using the recovery identity. Preserve their independent role and metadata. Generate one opaque32-hex `OPERATION` and two distinct fresh `swoovr_restore_<32hex>` database names.
2. Authenticate/decrypt before import; stage each role into its fresh quarantined target:

```sh
python3 scripts/backups/backup.py --store "$STORE" restore-quarantined --generation "$API_GENERATION" --target "$API_TARGET" --operation "$OPERATION"
python3 scripts/backups/backup.py --store "$STORE" restore-quarantined --generation "$KC_GENERATION" --target "$KC_TARGET" --operation "$OPERATION"
```

3. Stop source API, Keycloak and all source mutation jobs; verify they remain stopped. Point PGDATABASE at each corresponding source. Fence each using its corresponding generation and the same operation:

```sh
python3 scripts/backups/backup.py --store "$STORE" fence-source --generation "$SOURCE_GENERATION" --operation "$OPERATION" --confirm-source-stopped
```

4. Capture fresh current authority from each stopped/fenced source, after target staging. Use `create --authority-fence "$OPERATION" --confirm-source-frozen`, the correct role/tier/recipient and API bundle producer. These captures must remain less than one hour old through reconciliation. An old archive alone cannot prove subsequent completed deletions or new holds. If current authority cannot be recovered, keep both targets fenced and escalate; this tooling makes no promise to recover unknown later state or a disaster RPO.
5. Point PG connection settings at the recovery server. Reconcile the API using its newly captured authority bundle. This verifies current media, imports only current referenced bytes, runs hold-aware expiry, revokes prior API sessions and removes push device registrations before writing readiness:

```sh
python3 scripts/backups/backup.py --store "$STORE" complete-api --generation "$API_AUTHORITY" --target "$API_TARGET" --api-dll "$API_DLL"
python3 scripts/backups/backup.py --store "$STORE" replay-authority --generation "$KC_AUTHORITY" --target "$KC_TARGET"
python3 scripts/backups/backup.py --store "$STORE" complete-keycloak --generation "$KC_AUTHORITY" --target "$KC_TARGET" --paired-api "$API_TARGET"
```

6. Confirm both receipts belong to the same operation, inspect expiry/hold results and run the UAT smoke checks with isolated ingress. Promotion, DNS changes and deployment require owner approval. Do not restart original source jobs against a divergent database. Rollback also needs a positively selected authoritative source and current deletion/hold reconciliation; never clear a guard merely to start a host.

The operator-owned `swoovr_operations` schema is excluded from all archives. Importing a previously ready database cannot import serving approval. The actual API refuses fenced startup before migrations, workers or ingress. Paired Keycloak readiness requires the matching completed API receipt. Recovery never mints application authentication tokens.

## Evidence and limits

Local synthetic drills exercised actual PostgreSQL17, age1.3.2, application private local media and pinned Keycloak26.8.1. They proved authenticated decryption, corruption/wrong-key refusal, fresh-target quarantine, actual startup refusal, current deletion/hold replay, ordinary expiry, session/device reset and surviving/deleted fictional credentials. Azure SDK calls were tested against protocol fakes and real SDK signatures only. Bicep compilation proves syntax/types, not tenant policy, permissions, private networking, replication, scheduling, monitoring or hosted restoration. Complete an approved Azure restore drill before admitting UAT testers. See VALIDATION.md for exact commands/results and failed attempts.

Primary references: [age](https://github.com/FiloSottile/age), [PostgreSQL17 pg_dump](https://www.postgresql.org/docs/17/app-pgdump.html), [Azure lifecycle](https://learn.microsoft.com/en-us/azure/storage/blobs/lifecycle-management-overview).
