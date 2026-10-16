# User-selectable appearance themes

Status: confirmed product requirement, 16 October 2026.

## Purpose and reference

Every user can choose one of six approved colour directions for their own app interface. All six are available in light and dark mode. Appearance is a personal preference, not an app-wide administrator choice and not a paid entitlement.

Place this document beside **wanderconnect-colour-studio.html** in the project directory. The HTML is the visual reference for all six palettes, paired modes and sample screens. It is a design explorer, not production app code. The earlier muted palettes in the product brief are superseded by these six options. Earlier teal mockups remain layout references only.

The owner explicitly selected the six-palette personalisation approach. Defaults, storage and interaction details below are recommended implementation decisions; retain them unless the project has an established equivalent.

## Settings experience

Add **Settings → Appearance** with two independent controls:

1. **Colour scheme:** six named preview tiles showing primary, accent and surface colours. Each tile has an accessible selected indicator and name. Preview the real interface immediately when selected.
2. **Display mode:** **System**, **Light**, **Dark**. System tracks OS/browser changes; it must not be converted permanently into whichever mode happened to be active when selected.

Show a small sample card/button/chat bubble in the settings screen to help people assess their choice. All six schemes must be available in both modes without changing the selected scheme. Keep selection controls usable at large text sizes and with keyboard/screen readers. Colour is not the only selection indicator.

Recommended defaults: Electric violet + System. This default is proposed; all six alternatives are approved options. Offer **Reset appearance** to restore the default pair. Changes apply immediately and persist automatically with a discreet saving/error state; do not require an app restart. A failed cloud save must not silently discard the local choice. On narrow screens stack tiles rather than clipping them.

Appearance affects only the current user's UI. It does not recolour their public profile for other people, reveal their theme choice, change matching or modify permissions. Do not send a sender's theme colours in chat messages: the recipient renders messages in their own selected theme.

## Scheme identifiers

Use these stable IDs for persistence, not display names, array positions or hexadecimal values. Keep display names localisable.

| ID | Display name | Character |
| --- | --- | --- |
| `electric` | Electric violet | Violet + tangerine. Expressive, contemporary and energetic. My first direction to test. |
| `raspberry` | Raspberry spark | Raspberry + electric blue. Bold and sociable without making every surface pink. |
| `cobalt` | Cobalt social | Cobalt + bright apricot. Crisp, confident and broad enough for friendship, romance and travel. |
| `coral` | Coral current | Coral + deep ocean blue. Warm and open, with more daylight and less nightclub. |
| `orchid` | Orchid afterglow | Orchid + aqua. Playful and distinctive, especially strong in the darker theme. |
| `solar` | Solar club | Golden yellow + violet. The most unexpected direction: outgoing, optimistic and less conventionally dating-coded. |

## Design rationale

These are deliberately more vibrant than the initial brief. Saturated brand colours distinguish actions, selected states and highlights; quieter surfaces support reading and keep people/profile content prominent. Dark modes use separately chosen surfaces and foregrounds rather than colour inversion. The selected hue does not imply age, gender, sexual orientation or relationship intention.

Hue relationships, hierarchy and contrast informed the palettes. There is no claim that colour psychology guarantees trust, attraction or commercial success. Treat accents as decorative until any text or functional use is checked against its actual background.

## Canonical tokens

| Token | Intended use |
| --- | --- |
| bg | App/page background |
| surface | Cards, menus, panels and input surfaces |
| text | Main readable foreground |
| muted | Secondary text; not disabled opacity |
| primary | Primary action and selected-state fill |
| onPrimary | Text/icons on the primary fill |
| accent | Secondary decorative emphasis; not automatically safe as text |
| soft | Tinted selection, chip and contextual highlight fill |
| softText | Text/icons on soft fill; links may use this on other backgrounds only after checking |
| border | Subtle separators and decorative card boundaries |

Preserve foreground/background pairings. In particular, Solar club's light primary uses dark text, and all dark-mode primary buttons use the explicit onPrimary token. Never hardcode white text on every primary button.

### Complete palette values

#### Electric violet — `electric`

Contrasting violet/orange accents.

| Token | Light | Dark |
| --- | --- | --- |
| bg | `#F7F4FF` | `#171020` |
| surface | `#FFFFFF` | `#231A30` |
| text | `#201331` | `#F6F0FF` |
| muted | `#63536F` | `#C6B5D9` |
| primary | `#7132D0` | `#BE94FF` |
| onPrimary | `#FFFFFF` | `#25103E` |
| accent | `#FF8C42` | `#FFAF72` |
| soft | `#EEE3FF` | `#39234E` |
| softText | `#58239B` | `#DFC3FF` |
| border | `#DAD0E8` | `#564265` |

#### Raspberry spark — `raspberry`

Magenta/blue analogous family.

| Token | Light | Dark |
| --- | --- | --- |
| bg | `#FFF4F8` | `#1C101A` |
| surface | `#FFFFFF` | `#2C1B29` |
| text | `#321326` | `#FFF0F7` |
| muted | `#755265` | `#DAB6CA` |
| primary | `#BC1459` | `#FF85B5` |
| onPrimary | `#FFFFFF` | `#3C0B25` |
| accent | `#3F6FFF` | `#98B4FF` |
| soft | `#FFE0ED` | `#4E2039` |
| softText | `#951044` | `#FFBAD7` |
| border | `#E9CAD7` | `#674055` |

#### Cobalt social — `cobalt`

Blue/orange complementary contrast.

| Token | Light | Dark |
| --- | --- | --- |
| bg | `#F2F6FF` | `#10182B` |
| surface | `#FFFFFF` | `#1A2540` |
| text | `#142544` | `#EDF3FF` |
| muted | `#526581` | `#B6C5E1` |
| primary | `#2454D6` | `#8EAEFF` |
| onPrimary | `#FFFFFF` | `#10234E` |
| accent | `#FF9C57` | `#FFB882` |
| soft | `#E0E9FF` | `#263C65` |
| softText | `#1C42AA` | `#C2D4FF` |
| border | `#CDD8EE` | `#415477` |

#### Coral current — `coral`

Warm coral/cool blue counterpoint.

| Token | Light | Dark |
| --- | --- | --- |
| bg | `#FFF6F1` | `#211410` |
| surface | `#FFFFFF` | `#32201B` |
| text | `#35201C` | `#FFF1E8` |
| muted | `#795C52` | `#DABCAE` |
| primary | `#BE402C` | `#FF9C80` |
| onPrimary | `#FFFFFF` | `#45190F` |
| accent | `#168CBA` | `#77D4F5` |
| soft | `#FFE2D6` | `#512C23` |
| softText | `#8E2C1D` | `#FFC7B4` |
| border | `#E7CFC2` | `#6C493D` |

#### Orchid afterglow — `orchid`

Purple/green cool counterpoint.

| Token | Light | Dark |
| --- | --- | --- |
| bg | `#FBF2FF` | `#1B1023` |
| surface | `#FFFFFF` | `#2B1B36` |
| text | `#30163B` | `#FBF0FF` |
| muted | `#73537D` | `#D0B7DC` |
| primary | `#9430BC` | `#DB99F8` |
| onPrimary | `#FFFFFF` | `#351044` |
| accent | `#00B9B2` | `#67E5D5` |
| soft | `#F3DFFF` | `#472653` |
| softText | `#762194` | `#F0C3FF` |
| border | `#E3CDEB` | `#61436D` |

#### Solar club — `solar`

Yellow/violet complementary contrast.

| Token | Light | Dark |
| --- | --- | --- |
| bg | `#FFF9E9` | `#1C190F` |
| surface | `#FFFFFF` | `#2D2718` |
| text | `#30240E` | `#FFF7DF` |
| muted | `#6C5C39` | `#D3C69F` |
| primary | `#F4C52F` | `#F6CE50` |
| onPrimary | `#352600` | `#352600` |
| accent | `#7043CF` | `#C1A1FF` |
| soft | `#FFF0B9` | `#493B17` |
| softText | `#604500` | `#FFE28E` |
| border | `#E1D4AE` | `#675935` |

## Preference model and persistence

Store the stable scheme and requested mode separately. Effective mode is derived at runtime.

```csharp
public enum AppearanceMode { System, Light, Dark }

public sealed record AppearancePreference(
    string SchemeId = "electric",
    AppearanceMode Mode = AppearanceMode.System,
    int SchemaVersion = 1);
```

Validate scheme IDs and modes server-side and client-side. Unknown or removed values fall back safely to Electric violet + System; do not crash or leave a partly themed screen. Store enum names or explicit stable wire values, not implicit numeric enum ordinals. Do not accept arbitrary CSS or resource keys as preference values.

Recommended precedence:

- With an account, the last successfully synchronised account preference is the baseline across devices.
- Cache locally so the chosen appearance applies before profile/API data finishes loading.
- A pending local change wins on that device while offline and is synchronised when authenticated connectivity returns.
- Resolve conflicts using a server version/ETag and latest accepted explicit change, not untrusted client clock ordering. A stale background sync must not overwrite a newer selection; rebase or surface a conflict when necessary.
- Guests can select locally. At first sign-in use an existing account preference; if none exists, initialise it from the guest choice. Do not replace another account's preference with the previous account's cached choice.
- On sign-out, remove account-specific cached settings. A generic guest preference may remain only by deliberate policy.

Suggested endpoint: GET/PATCH /api/me/appearance. The API derives the account from authentication. Only the user or an explicitly authorised support workflow can change their preference. Appearance does not belong in subscription entitlements or identity claims.

The same account scheme can sync across web and mobile; System naturally resolves differently on devices with different OS modes. A per-device override is optional future scope, not necessary for the initial feature.

## Blazor implementation

Use semantic CSS custom properties in a shared theme stylesheet. Apply a stable scheme attribute and the **resolved** mode to the root element. No need to copy the entire HTML explorer into the application.

```html
<html data-scheme="electric" data-theme="dark">
```

```css
:root[data-scheme="electric"][data-theme="light"] {
  --bg: #F7F4FF;
  --surface: #FFFFFF;
  --text: #201331;
  --muted: #63536F;
  --primary: #7132D0;
  --onPrimary: #FFFFFF;
  --accent: #FF8C42;
  --soft: #EEE3FF;
  --softText: #58239B;
  --border: #DAD0E8;
  color-scheme: light;
}

:root[data-scheme="electric"][data-theme="dark"] {
  --bg: #171020;
  --surface: #231A30;
  --text: #F6F0FF;
  --muted: #C6B5D9;
  --primary: #BE94FF;
  --onPrimary: #25103E;
  --accent: #FFAF72;
  --soft: #39234E;
  --softText: #DFC3FF;
  --border: #564265;
  color-scheme: dark;
}

body { background: var(--bg); color: var(--text); }
.profile-card { background: var(--surface); color: var(--text); }
.primary-action { background: var(--primary); color: var(--onPrimary); }
.intention-chip { background: var(--soft); color: var(--softText); }
```

Generate equivalent selectors for every pair from the canonical data. Resolve System using matchMedia('(prefers-color-scheme: dark)') and subscribe to change events only as appropriate; dispose listeners. Apply a cached preference before first paint with a minimal CSP-compatible bootstrap to avoid a light flash. SSR/public pages need an explicit default or safe preference cookie strategy; user-specific rendered markup must not leak through shared caches. WASM code is not authoritative for authentication or entitlements.

The HTML explorer's **Download selected tokens** exports only that scheme's mode tokens. It does not export all scheme selectors, account persistence or production accessibility states. Keep theme data in a canonical JSON/C# definition and generate CSS rather than hand-maintaining inconsistent copies.

## MAUI implementation

Use a shared theme service to resolve scheme/mode and update application ResourceDictionary entries on the UI thread. Components reference DynamicResource keys for runtime switching. An AppThemeBinding alone handles mode variation but not all six independent schemes; the service must manage both dimensions.

```xml
<Button Text="Message"
        BackgroundColor="{DynamicResource ThemePrimary}"
        TextColor="{DynamicResource ThemeOnPrimary}" />

<Border BackgroundColor="{DynamicResource ThemeSurface}">
    <Label Text="About me"
           TextColor="{DynamicResource ThemeText}" />
</Border>
```

Map bg→ThemeBackground, surface→ThemeSurface, text→ThemeText, muted→ThemeMuted, primary→ThemePrimary, onPrimary→ThemeOnPrimary, accent→ThemeAccent, soft→ThemeSoft, softText→ThemeSoftText, border→ThemeBorder.

Persist the local preference using the project's settings abstraction, and synchronise with the account service. React to requested system appearance changes. Update status/navigation bar contrast where supported. Native provider login sheets and store purchase screens follow their platform/provider styling; don't force brand colours onto system-owned interfaces.

Share stable scheme IDs, tokens and preference DTOs between clients where appropriate. Share theme semantics, not Razor markup with XAML.

## Component states and accessibility

The supplied HTML calculates **72 text contrast checks**: six pairs × light/dark × six schemes. Each measured pair meets 4.5:1 in the supplied values: text/bg, text/surface, muted/bg, muted/surface, onPrimary/primary and softText/soft. These are solid-colour normal-text checks, not full WCAG certification.

The accent, border, hover, focus, disabled, gradients, image overlays and other combinations are not covered by that result. In particular, a decorative border token may be too subtle to be the only visible boundary for an input or actionable control. Derive and validate stronger control-border/focus tokens where required. Do not label the app “fully accessible” from palette tests alone.

Implementation requirements:

- Test normal text ≥4.5:1 and qualifying large text ≥3:1 against actual surfaces.
- Test required non-text control indicators and focus states against applicable adjacent colours; use the relevant accessibility guidance.
- Define hover/pressed/selected/disabled/focus states for each pair and validate them. Do not apply arbitrary opacity to whole controls.
- Define distinct semantic success/warning/error/information tokens per mode, with labels/icons. These states must not change meaning when switching schemes.
- Never equate pink with romance, blue with friendship or any palette with a user's identity. If connection categories use colour, keep that mapping explicit and accessible across themes.
- Profile media remains original colour. Add accessible backing surfaces for text placed over images; gradients alone are not a contrast guarantee.
- Preserve reduced motion, text scaling, touch targets, keyboard focus and screen-reader names.
- Avoid animated whole-screen colour transitions. Honour reduced-motion preferences.
- Treat existing teal images as samples, not assets to recolour and ship as working screens.

## Acceptance criteria

1. All six named schemes appear in Appearance settings and expose light/dark previews.
2. Every scheme works with System, Light and Dark: 18 preference combinations, 12 distinct resolved visual themes.
3. Scheme changes apply instantly across discovery, profiles, chat, plans, vouchers, dialogs, menus, settings and error states.
4. Switching mode retains the selected scheme; switching scheme retains the requested mode.
5. System responds to OS/browser changes; explicit Light/Dark ignores those changes.
6. Refresh/restart restores the cached setting without a disruptive wrong-theme flash.
7. Account sync and offline retries do not overwrite newer choices or leak settings across accounts.
8. Unsupported values fail safely; there are no uninitialised resource keys or unreadable controls.
9. Contrast tests cover the supplied pairs and all additional interactive/semantic states used in production.
10. The recipient's theme renders their own messages/profile views; appearance settings never affect eligibility, visibility or billing.
11. Light/dark provider sheets remain legible and conform to their provider's branding requirements.
12. Settings work at large text sizes, narrow widths and with keyboard/screen reader interaction.

## Handoff instruction for the implementing AI

Implement this as a real user preference, not a design-time palette selector. Read the adjacent HTML to understand each visual direction. Extract the canonical values above, build one shared appearance contract, wire Settings → Appearance, implement both client renderers and persist/synchronise the preference. Preserve existing app functionality and privacy rules. Do not add a subscription lock or infer dating intentions from theme choice. Add focused tests for preference validation, resolution, persistence and contrast. Document any integration assumptions rather than inventing completed account services.
