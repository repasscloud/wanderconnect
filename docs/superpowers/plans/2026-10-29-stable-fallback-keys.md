# Stable legacy fallback key derivation

Issue118 prerequisite, existing D187/D189/D191/D228 key retention. DEFAULT: every legacy version1/unversioned fallback derives only from founder secret1 with its existing purpose label. Adding founder secret2 continues to issue founder coupons under2, but cannot relabel an unrelated derived key as encryption1 or change existing lookup fingerprints. Missing secret1 disables fallback; never guess an old source. Explicit keys take precedence. Development push retains derived encryption1 alongside explicit newer keys until deliberate migration.

1. Add actual API-consumer configuration/crypto regression checks to the existing Checks project (architecture permits tests to reference layers under test; no new project). Demonstrate old-code failure after adding founder2.
2. Centralise legacy derivation on Founders and migrate evidence/contact/identity encryption and matching, phone codes, terminated-contact holds, media links and Development push. Preserve exact labels/bytes for existing secret1 installations.
3. Test decryption/matching/phone challenge/media signatures/device protection, explicit precedence and missing1; add real PostgreSQL legacy-hold lookup if needed. Document manual recovery for historical mislabelled keys and hosted explicit-key requirements.
4. Relevant browser regressions, check.sh, source review, main host restoration, one commit, explicit refspec push, draft PR/issue118 update. No merge/deploy/Actions or private key logging.

Old-code red reproduced, implementation and17 actual API checks/55 PostgreSQL checks pass. Relevant browser suites pass; no schema/UI/native change. Read-only review found no concrete defect. Final gate/55 PostgreSQL and startup14 pass; main restoration and scoped commit/push/PR/issue118 update complete the slice.
