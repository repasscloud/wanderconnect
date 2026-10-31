# Swoovr API contract versions

CONFIRMED D211 (Q83); DEFAULT selection policy D222, 29 October 2026. Swoovr endpoints use stable paths, for example `POST /api/me/devices`, with `Swoovr-Version: 2026-10-28`. Contract dates identify published breaking contracts, not deployment or request dates. Numerical URL version prefixes are prohibited on Swoovr routes.

## Selection and compatibility

`Swoovr.Contracts.ApiContract` is the single supported-version registry. Initial/current contract: `2026-10-28`. Middleware applies to the `/api` path segment, before authentication and account side effects, including public resources, media and webhooks.

| Request | Result |
| --- | --- |
| Header absent | Initial contract `2026-10-28`, permanently pinned even when Current advances |
| Exactly one supported `YYYY-MM-DD` | That contract |
| Empty, whitespace, multiple values, comma list, malformed format or invalid calendar date | HTTP 400 `invalid-api-version` |
| Valid calendar date not in Supported | HTTP 400 `unsupported-api-version` |

Errors use `application/problem+json`, contain the code and supported dates, never echo supplied input, and use `Cache-Control: no-store`. Selected responses carry `Swoovr-Version`; all API responses vary by that header. This header does not authenticate, authorise or bypass any member/staff gate. Legacy numerical paths are unregistered (404 with an otherwise valid/default contract).

Missing-header compatibility supports browser image loads and external provider webhook senders that cannot add Swoovr headers. Their signatures, expiry and access rules still apply. A supplied invalid header is rejected consistently even on those routes. Health endpoints and `/hubs/messages` use their own protocols and are outside this REST contract. SignalR and `/bff/hubs/messages` paths are unchanged.

## Clients and relays

All Swoovr typed HTTP clients pin Current explicitly through `ApiContractClient.Configure`; an explicitly configured date is preserved. This covers WebAssembly/BFF, native MAUI, server-side member/status/safety and Admin clients, including downloads. Keycloak OIDC and other provider HTTP clients do not receive this configuration.

The member BFF keeps its existing route allow-list, session and `X-Swoovr-Bff` requirements. It forwards an explicit date unchanged and returns the API's selected date; malformed/unsupported dates are not silently replaced. Its server-side requests pin Current when the browser omits a date. Media uses the pinned server client. No admin route is added to the member relay.

## Publishing a breaking contract

DEFAULT: add a date to Supported only together with explicit version-specific dispatch/DTO projection and compatibility tests for every previously supported date. Do not merely change Current or add an accepted date while routing old clients to a new incompatible shape. Select the request version from middleware/response contract and keep the old behavior functioning. Add release notes, client upgrade guidance and a documented retirement window before proposing any removal. No version retirement is authorised here; initially there is one contract.

Earlier dated delivery evidence, decisions and owner quotations retain their historical URLs as provenance. Current runtime paths, tests and runbooks use the stable paths. Third-party APIs (including Mailpit and FCM HTTP v1) retain their provider-owned URLs.

Validation: `scripts/smoke.py`, `ApiContractChecks`, typed transport checks and `tests/api/versioning.browser.cjs`; full route migration also requires the runbook's feature browser suites. See [validation](../delivery/VALIDATION.md).

## Operator cutover

No Swoovr API was hosted at this slice's baseline. Rebuild/distribute local mobile clients together with the server cutover; earlier local binaries using numerical paths must be upgraded. Regenerate short-lived media/export links. Configure provider callbacks, especially Didit's destination, to `/api/webhooks/didit` and prove a real signed delivery when authorised. Repository edits do not alter provider consoles or the owner's tunnel.
