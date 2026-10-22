# Swoovr naming conventions

Updated 22 October 2026. CONFIRMED by the owner; [D30](../delivery/DECISIONS.md#naming-confirmation--22-october-2026) supersedes the original brand-neutral namespace rule.

## What to use and where

| Surface | Convention / value | Source of truth |
| --- | --- | --- |
| Product, headings, navigation, app title | Swoovr | Product brief and this document |
| .NET namespaces, projects, assemblies | `Swoovr.Api`, `.Web`, `.Mobile`, `.Contracts`, `.Client`, `.Domain`, `.Infrastructure` | `src/Swoovr.*` project files |
| Full / portable solutions | `Swoovr.sln` / `Swoovr.Local.sln` | Repository root |
| Executable policy checks | `Swoovr.Checks` | `tests/Swoovr.Checks` |
| JavaScript browser globals | `SwoovrPalettes`, `SwoovrAppearance`, `SwoovrGuestAppearance` | Generated palette and appearance modules |
| Guest appearance local storage | `swoovr.appearance.guest.v1` | `src/Swoovr.Web/wwwroot/appearance-core.js` |
| Browser test environment variables | `SWOOVR_PLAYWRIGHT_PATH`, `SWOOVR_WEB_URL`, `SWOOVR_BROWSER_OUTPUT` | Optional browser drivers under `tests/` |
| Local Compose project / realm | `swoovr-local` / `swoovr-dev` | `compose.yaml`, `containers/keycloak/swoovr-dev-realm.json` |
| Investigation OAuth client | `swoovr-investigation` | `scripts/auth/security.mjs` |
| Investigation cookie / resource markers | Lowercase `swoovr` prefix | `scripts/auth/` |
| Mobile application ID | `com.swoovr.app` (DEFAULT development identifier) | `src/Swoovr.Mobile/Swoovr.Mobile.csproj` |
| Colour studio / exports | `swoovr-colour-studio.html`, `swoovr-…-tokens.css` | `docs/design/` |

Use PascalCase `Swoovr` in C#/XAML types and product prose, lowercase `swoovr` in machine identifiers and filenames, and uppercase `SWOOVR_` for shell environment variables. Preserve existing suffixes and casing required by the platform. Do not rename third-party technologies such as Keycloak.

## Why and compatibility

The owner selected one name across product and engineering to remove the former split. This is a source and local-development identity change, not a completed authentication or deployment integration. API routes and safety rules retain their behavior; the status service label is now `Swoovr.Api`.

Guest preferences start under the new storage key; old browser data is not migrated or deleted. Existing local realms, sessions, investigation fixtures and Compose volumes are not migrated or deleted. Follow the [migration record](../delivery/SWOOVR-RENAME-2026-10-22.md) before restarting the sandbox. Mobile signing, store registrations and trademark clearance remain OPEN; the development ID is not proof of ownership or availability.

Real GitHub repository/PR URLs and recorded absolute filesystem paths retain their actual spelling. Git history, old binary screenshots and original artwork are historical evidence, not current UI. Do not fabricate renamed URLs, edit Git internals or rewrite private fixtures to make a text search empty.

MAUI metadata follows the official [project configuration](https://learn.microsoft.com/dotnet/maui/deployment/visual-studio-properties?view=net-maui-10.0#maui-shared) and [app information](https://learn.microsoft.com/dotnet/maui/macios/info-plist?view=net-maui-10.0#provide-app-info) documentation: `ApplicationTitle` supplies the display name and `ApplicationId` supplies app identity. This source review does not verify signing or store registration.
