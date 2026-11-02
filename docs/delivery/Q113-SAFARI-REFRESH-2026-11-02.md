# Q113: Admin refresh after SSR forms

Issue: [#158](https://github.com/repasscloud/wanderconnect/issues/158), Q113, intake item 21. Owner reports private Safari 27.2 (22625.2.8.11.9), refresh asking to resend a form on any admin page, independently of timing; navigation works.

## Change and evidence

DEFAULT engineering remedy: opt the existing named Razor SSR forms into Blazor enhanced form handling (`data-enhance="true"`). App.razor already loads blazor.web.js. Submissions remain server-side POST requests, with their existing named handlers, antiforgery tokens, API permissions and fresh-session enforcement. Blazor fetches the response and patches the current GET document, preserving immediate validation feedback, selected reasons and explicit preview/reveal/conceal results without adding native POST document history. Refresh subsequently loads the current route as GET. It does not replay an approval, search or access-record action.

[Microsoft's .NET 10 navigation guidance](https://learn.microsoft.com/aspnet/core/blazor/fundamentals/navigation?view=aspnetcore-10.0#enhanced-navigation-and-form-handling) documents per-form opt-in, fetched DOM updates and the restriction to Blazor endpoints. The non-Blazor sign-out endpoint retains its native form and redirect; sign-in and download behavior is unchanged. No global redirect or authentication/antiforgery exception is introduced.

Source diagnosis: MediaReview.razor reloads the queue before dispatching dynamic item-bound named forms. An approved item disappears, so native POST replay may target a form no longer rendered. The owner's resend prompt supports this explanation, but no native Safari network trace has been captured here.

## Verification scope

Extended `tests/admin/media-review.browser.cjs` records document navigation methods; proves submitted validation selection remains present; exercises blurred preview, reveal and conceal; repeats GET reloads after access and after decisions; and checks persisted access/audit counts to detect duplicate effects. Success waits target removed item rows rather than matching a previous response's status message.

Implementation handoff: these new checks have not yet run. Coordinating agent must run syntax validation, the isolated admin browser harness and `./scripts/check.sh` before claiming passing evidence or committing. No service, browser or build was started by this implementation subtask.

OPEN acceptance: actual private Safari 27.2; back/forward and simultaneous reviewers; expired-session renewal and non-Blazor download/sign-out regression verification. Chromium/Edge acceptance cannot establish native Safari acceptance.

JavaScript-disabled or failed-script submissions retain native POST fallback and its resend prompt. Existing native POST history is not rewritten: navigate once through a fresh GET after updating. Refresh intentionally resets ephemeral preview/reveal and validation state rather than repeating their POST handlers. Enhanced submission preserves those states in its immediate response; it does not persist them across refresh.

Actual owned Edge/Keycloak/API/Admin test passed: rendered validation selection, blurred preview, reveal/conceal and exact access counts, successful removal and repeated GET reload without duplicate decisions. Owned hosts/browser/database/client/fictional staff cleanup passed. `node --check` passed. Repository `check.sh` integration-red-check passed initial gates and compiled application projects, then failed at intentional RED Q108 tests for absent ModeLimit/PlacementAllowed; persistence/smoke not reached. This is a documented integrated-gate gap, not a full pass. Native private Safari27.2, no-JS submission and old POST-history recovery remain as stated above.
