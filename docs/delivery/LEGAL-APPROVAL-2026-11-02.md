# Legal approval — 2 November 2026

Status: **CONFIRMED D288**, owner statement in this session: “Legal has approved this ready to go.” Approval applies to the owner-supplied revisions listed below. This records the owner’s confirmation, not an independent legal opinion or a security/app-store audit.

The approved wording includes terms/community guidelines, privacy notice Draft 0.17, retention, internal evidence protection, police disclosure, breach response and the Code/register revisions. Existing draft filenames and runtime draft status are retained in this commit: publication, effective-version selection and acceptance activation require a separate implementation change. Older pending-review labels in the supplied documents describe their preparation state; this dated approval record supersedes that legal-review prerequisite for these exact revisions.

Legal approval does not establish deployed behavior, statutory or Code compliance, signatory status, appointed staff, real reporting/monitoring, hosting location, processor arrangements or completed security/app-store review. Newly described obligations and architecture must be reconciled against actual implementation before release. In particular, Sydney processing/regional storage replicas and mandatory statutory controls are approved policy requirements, not evidence that infrastructure or controls are deployed.

## Approved source snapshot

SHA-256 identifies the owner-supplied text before this approval record was added. The shared public-policy source hash remains checked by the existing suite.

| Source | SHA-256 |
| --- | --- |
| [docs/delivery/BREACH-RESPONSE-RUNBOOK.md](../../docs/delivery/BREACH-RESPONSE-RUNBOOK.md) | `016d7485c2b04945dc56ff303fb8db705b7c4f882b5c5ecd2adb062a9103a09b` |
| [docs/delivery/CODE-COMPLIANCE-REGISTER-DRAFT.md](../../docs/delivery/CODE-COMPLIANCE-REGISTER-DRAFT.md) | `0437008bc6955598ea12ba8bc1dbe57613d14b1b8dc4a0d3fa4e49ed7593ba21` |
| [docs/delivery/INTERNAL-TRUST-SAFETY-EVIDENCE-PROTECTION-POLICY.md](../../docs/delivery/INTERNAL-TRUST-SAFETY-EVIDENCE-PROTECTION-POLICY.md) | `6e6a4c2f5931457be6721a198e33bc4f4f40320c978904c602f59336a46c63aa` |
| [docs/delivery/ONLINE-DATING-CODE.md](../../docs/delivery/ONLINE-DATING-CODE.md) | `3840787196161384a636d228d0c58f13027917b9f2bd29c710e61b39c6fb5a21` |
| [docs/delivery/POLICE-CONTACT-PROCEDURE.md](../../docs/delivery/POLICE-CONTACT-PROCEDURE.md) | `c23d7a592692ec5071459588a8b6bc6f4c09449f3d1dc88b4845cf71e65890c3` |
| [docs/delivery/RETENTION-SCHEDULE.md](../../docs/delivery/RETENTION-SCHEDULE.md) | `7a41fddbb8647ba816951b07e7ef5c23df1fe0bae8238dd6e1eb3487a900b456` |
| [docs/delivery/TERMS-AND-COMMUNITY-GUIDELINES-DRAFT.md](../../docs/delivery/TERMS-AND-COMMUNITY-GUIDELINES-DRAFT.md) | `8c2a55247543aa711d239e21d200dbbe9dd0020afeb1964f8a1d787523b4a1e9` |
| [src/Swoovr.Client/PrivacyNotice.cs](../../src/Swoovr.Client/PrivacyNotice.cs) | `26cc0891fabfda0377643010979248d7a5ec5282a0276ae2ad6fcafe5108d948` |
| [src/Swoovr.Client/PublicPolicyDrafts.cs](../../src/Swoovr.Client/PublicPolicyDrafts.cs) | `6e8a47462524e920144ada03822c9848dc5c31dbd1a140ab2da2a91f2cbe470a` |

## Issue mapping

- #57: legal/privacy document approval recorded; application-security and app-store-policy acceptance remain separate.
- #22, #99: founder-token, identity, ban matching and retention disclosures updated; operational checks are not certified here.
- #83–#92: revised internal policy and retention obligations inform each policy implementation issue; implementation and launch evidence remain independent.
- #55, #113, #118, #119, #120, #121: revised Code gap analysis/register, terms, retention and policy notices. Existing closed issues are not reopened; organisational work and actual publication remain separate.

No issue is closed solely by this approval record. No deployment, migration, acceptance activation or merge is performed.
