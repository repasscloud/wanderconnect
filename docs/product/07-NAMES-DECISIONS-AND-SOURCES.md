# Names, decisions and sources

## Confirmed name — 22 October 2026

CONFIRMED, owner: **Swoovr** is the product name. Use `Swoovr` for display text, .NET namespaces, projects and assemblies; `swoovr` for lowercase identifiers. The owner explicitly requested the entire codebase to follow this convention, superseding the earlier neutral namespace and naming shortlist.

[Naming conventions](../architecture/NAMING.md) define each identifier. [Migration record](../delivery/SWOOVR-RENAME-2026-10-22.md) explains what changed, why, how to run it, retained historical references and validation gaps. Name selection does not establish trademark clearance, app-store availability or registered signing identifiers; those publishing checks remain OPEN under Q08.

## Decisions and defaults

| Topic | Status | Current treatment |
| --- | --- | --- |
| Broad adult connection platform | CONFIRMED | Not solely travel/dating |
| Photo optional / sparse filter | CONFIRMED | Distinct from verification |
| Required and excluded intentions | CONFIRMED | Backend enforcement |
| Incognito acceptance by profile owner | CONFIRMED | No bypass |
| Travel no early initiation | CONFIRMED | Presence-gated |
| Reply to local before arrival | CONFIRMED, owner 23 October 2026 (D60) | Travellers may reply to a local's conversation before arrival, not start one |
| Venue voucher experiences | CONFIRMED direction | Commercial mechanics are defaults |
| Apple/Google sign-in and social links | CONFIRMED direction | Specific other providers/links are defaults |
| MAUI + Blazor + shared API | CONFIRMED direction | Eight projects since Web.Client (D59); validate vertical slice |
| Name | CONFIRMED, owner 22 October 2026 | Swoovr; publishing clearance remains OPEN |
| Appearance | CONFIRMED | Eight free personal schemes, light/dark; Standard + System is the default (owner design handoff, 22 October 2026) |
| Keycloak | CONFIRMED | Authentication backend; integration contract unverified |
| Privileged login | CONFIRMED | Venue partner/admin/support: dedicated email/password or email link; no social login |
| MFA | CONFIRMED | Optional |
| Member magic link | CONFIRMED, owner 22 October 2026 (D34) | Built inside Keycloak, not yet implemented; passkeys confirmed (D33) |
| Identity gate/method | OPEN details | Default before browsing/contact |
| Pricing/premium allocation | CONFIRMED, owner 23 October 2026 (D63) | Free/Plus/Premium AUD prices; Free only during beta |
| Launch city/countries | CONFIRMED, owner 23 October 2026 (D62) | Private Adelaide beta, about 100 testers |
| Venue merchant/settlement model | CONFIRMED direction, owner 23 October 2026 (D61) | Per-vendor models, Stripe first; real money off until validated |
| “Whisper” comparator | UNRESOLVED | No invented identification |
| Full requirement set | Owner review pending | Defaults permit prototype progress |

## Material source notes — checked 16 October 2026

Official sources inform platform/technical constraints, not invented market success claims. Re-check before production; policies and workloads change. Previous conversation concepts are owner discussion inputs, not competitor research evidence.

- Apple App Review Guidelines: https://developer.apple.com/app-store/review/guidelines/ — review 1.1.4, 1.2, 3.1, 4.3, 4.8 and account deletion requirements. No promise that a broad platform containing casual-intent features is automatically accepted.
- Google Play Payments: https://support.google.com/googleplay/android-developer/answer/9858738?hl=en — digital services versus physical services and regional programmes.
- Google Payments FAQ: https://support.google.com/googleplay/android-developer/answer/10281818?hl=en — consumption-only access and external purchase communications.
- Microsoft Blazor render modes: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes — SSR and Interactive WASM.
- Microsoft MAUI billing sample: https://learn.microsoft.com/en-us/samples/dotnet/maui-samples/cross-platform-billing-service/ — implementation reference, verify maintenance/version compatibility before adoption.
- Microsoft native interop: https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/invoke-platform-code — native platform access from MAUI.
- Breeze venue partners: https://www.breeze.social/become-partner — reference for organised meetings in partner locations, not the owner's confirmed comparator.
- Breeze FAQ: https://www.breeze.social/faq — operational reference, not terms to copy.

## Owner review

Use the ordered [question backlog](../delivery/QUESTIONS.md), [decision process](../delivery/DECISIONS.md) and [phases](../delivery/PHASES.md). Do not ask the owner to select a single global palette: all six are approved personal options. The name is confirmed. Identity verification, pilot operations and commerce decisions remain separate.
