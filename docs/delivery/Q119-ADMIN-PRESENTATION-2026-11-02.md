# Q119 / issue164 — Admin presentation inventory

Reuses existing Standard tokens/CSS. Removes rendered D/ticket references and routine lookup/reveal narration; source comments, actual identifiers, auditing and authorisation remain. High-risk guidance uses one shared contextual panel. The UI is not evidence of a live provider or completed review.

| Route | Source | Presentation / intentional exception |
| --- | --- | --- |
| `/access` | `Access.razor` | Contextual legal/security guidance retained |
| `/accounts/{Id:guid}` | `Account.razor` | Shared heading, controls, status and responsive table conventions |
| `/accounts` | `Accounts.razor` | Shared heading, controls, status and responsive table conventions |
| `/appeals` | `Appeals.razor` | Shared heading, controls, status and responsive table conventions |
| `/beta` | `BetaEnrolments.razor` | Shared heading, controls, status and responsive table conventions |
| `/beta-scorecard` | `BetaScorecard.razor` | Shared heading, controls, status and responsive table conventions |
| `/cases/{Id:guid}` | `Case.razor` | Shared heading, controls, status and responsive table conventions |
| `/cases` | `Cases.razor` | Shared heading, controls, status and responsive table conventions |
| `/code-reports` | `CodeReports.razor` | Shared heading, controls, status and responsive table conventions |
| `/contact-history` | `ContactHistory.razor` | Shared heading, controls, status and responsive table conventions |
| `/cases/{Id:guid}/content` | `ContentReview.razor` | Shared heading, controls, status and responsive table conventions |
| `/disclosures` | `Disclosures.razor` | Contextual legal/security guidance retained |
| `/dob-history` | `DobHistory.razor` | Shared heading, controls, status and responsive table conventions |
| `/dob-reviews` | `DobReviews.razor` | Shared heading, controls, status and responsive table conventions |
| `/holds/{Id:guid}` | `Hold.razor` | Contextual legal/security guidance retained |
| `/holds` | `Holds.razor` | Contextual legal/security guidance retained |
| `/` | `Home.razor` | Shared heading, controls, status and responsive table conventions |
| `/identity` | `Identity.razor` | Contextual legal/security guidance retained |
| `/law-enforcement` | `LawEnforcement.razor` | Contextual legal/security guidance retained |
| `/law-enforcement/{Id:guid}` | `LeCase.razor` | Contextual legal/security guidance retained |
| `/law-enforcement/transparency` | `LeTransparency.razor` | Shared heading, controls, status and responsive table conventions |
| `/media` | `MediaReview.razor` | Shared heading, controls, status and responsive table conventions |
| `/registration` | `RegistrationSettings.razor` | Shared heading, controls, status and responsive table conventions |
| `/registry` | `Registry.razor` | Shared heading, controls, status and responsive table conventions |
| `/registry/{Id:guid}` | `RegistryEntry.razor` | Shared heading, controls, status and responsive table conventions |
| `/accounts/{Id:guid}/reinstate` | `ReinstateAccount.razor` | Shared heading, controls, status and responsive table conventions |
| `/safety` | `SafetyResources.razor` | Shared heading, controls, status and responsive table conventions |
| `/safety/{Code}` | `SafetyResources.razor` | Shared heading, controls, status and responsive table conventions |
| `/security` | `Security.razor` | Contextual legal/security guidance retained |
| `/accounts/{Id:guid}/standing` | `Standing.razor` | Shared heading, controls, status and responsive table conventions |
| `/support` | `SupportLookup.razor` | Shared heading, controls, status and responsive table conventions |
| `/cases/{Id:guid}/text-review` | `TextReview.razor` | Shared heading, controls, status and responsive table conventions |

Root/Legal capabilities, destructive confirmations, specialist media warnings, environment/provider statuses and beta invitation-versus-delivery distinctions remain operational exceptions. No Bootstrap dependency is introduced: the current app has no Bootstrap bundle.

Verification pending: owned presentation/browser and full repository gates. Actual physical Safari, screen-reader/zoom acceptance remains separate.

Final owned Admin presentation scenario passed at375/768/1440px, including concise contextual-copy assertions, with actual local Keycloak/API/PostgreSQL and SSR. Log `admin-presentation-final.log`, exit0 and successful cleanup. This artifact predates only the final nested sanction-lift form enhancement; that separate Q113 source change is verified by the subsequent full gate. Physical devices and human assistive-technology testing remain open.

Pre-commit integrated `./scripts/check.sh` passed (exit0), including the actual disposable PostgreSQL suite and all three HTTP smoke stages. This is combined-branch validation; issue-specific browser/native/provider gaps remain explicitly identified above.
