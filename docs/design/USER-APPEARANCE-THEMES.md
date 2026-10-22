# Swoovr colour system and user appearance themes

Status: confirmed direction, 22 October 2026. **Supersedes** the six-palette version of 16 October 2026 (Electric violet, Raspberry spark, Cobalt social, Coral current, Orchid afterglow, Solar club) and the earlier teal/indigo/terracotta directions.

Source of truth: the `design/` folder of the **swoovr-marketing** repository. Copy the whole folder to wherever a project needs it (for the app: `docs/design/`); don't edit copies, change it in swoovr-marketing and copy again.

| File | What it is |
| --- | --- |
| `USER-APPEARANCE-THEMES.md` | This spec: the schemes, tokens, rules and implementation contract. |
| `MARKETING-COLOUR.md` | How posts, videos and the website use the palette. |
| `themes.json` | **Canonical** scheme data (8 schemes × light/dark, 10 tokens each, plus hero/stripes). |
| `brand-colours.json` | **Canonical** fixed colours: marketing neutrals, VIP metals, badges, status, flags, legacy video backgrounds. |
| `swoovr-colour-studio.html` | Live reference. Open in a browser: pick a scheme and mode, see phone previews, copy hex values, download CSS. Works offline. |
| `swatch.png` / `swatch.svg` | The whole palette on one sheet. |
| `tokens.css` / `tokens.ts` | Generated copies for web and TypeScript (Remotion) code. |
| `build.py` | Validates contrast and regenerates every copy. |

## Direction

Swoovr is a **black and white app**. The default look is ultra modern, professional, clean and crisp: off-black, off-white and greys, with colour coming from people's photos and the verification badges. Colour schemes are a **second view**: each person can turn one on in **Settings → Appearance**. Nobody has to.

Marketing follows the same order: black and white first, colour as the `-Colour` variant (see `MARKETING-COLOUR.md`).

## Settings → Appearance

Two independent controls:

1. **Colour scheme:** eight named tiles, grouped Core, Community, Colour. Each shows its light and dark colours, has a visible selected indicator that isn't colour alone, and previews the real interface immediately.
2. **Display mode:** **System**, **Light**, **Dark**. System tracks the OS/browser; it is never silently converted into whichever mode was active when chosen.

Default: **Standard + System**. Offer **Reset appearance** to return to it. Changes apply instantly and persist automatically (discreet saving/error state, no restart). Show a small sample (message, button, chip) on the settings screen. Stack tiles on narrow screens; keep it usable at large text sizes, with keyboard and screen readers.

A scheme affects only that person's own screens. It never recolours their profile for anyone else, never travels with chat messages, isn't visible to others, and never affects matching, visibility, permissions or billing. It is not a paid feature.

## Schemes

Persist these stable IDs, never display names, positions or hex values. Display names are localisable.

| ID | Name | Group | Character |
| --- | --- | --- | --- |
| `standard` | Standard | Core | Off-black and off-white. The default: ultra modern, clean and crisp. Photos and badges carry the colour. |
| `lounge` | Lounge | Core | Midnight navy, champagne gold and silver: the VIP look of swoovr.com/referral. Premium, calm, after-dark. |
| `pride` | Pride | Community | Neutral surfaces with a rainbow signature (Gilbert Baker six-stripe, 1979). Violet carries actions so text stays readable. |
| `trans` | Trans | Community | Light blue, pink and white from Monica Helms' transgender Pride flag (1999), with dark labels on the blue. |
| `union` | Union Jack | Community | Royal navy, red and white in the Union Flag style. Crisp, classic, a little cheeky. |
| `violet` | Violet | Colour | The logo's own violet with a tangerine accent. Expressive; the classic Swoovr video colour. |
| `coral` | Coral | Colour | Warm coral with an ocean-blue accent. Sunset on the foreshore: open, friendly, daylight. |
| `eucalyptus` | Eucalyptus | Colour | Gum-leaf green with a wattle-gold accent. Calm, grown-up and outdoorsy; suits every age. |

8 schemes × System/Light/Dark = 24 preference combinations, 16 resolved themes.

Where the colours come from:

- **Standard** uses the marketing neutrals already in every Swoovr video and post (ink `#0A0A0A`, muted `#6B6B6B`, line `#E5E5E5`, soft `#F4F4F4`) with an off-white page `#FAFAFA` so white cards lift off it. Warmer alternatives from usefulstash (carbon `#0D0F12`, paper `#F6F4EF`) are recorded in `brand-colours.json` but not used in the app: Swoovr stays neutral, not warm.
- **Lounge** is the swoovr.com referral page (`src/styles/referral.css`): navy `#1E3857`, deep navy `#172C45`, gold `#D0B786`, silver `#C1C1C1`, graphite `#454545`, ivory `#F0ECE4`. Dark mode is the page itself; light mode puts navy on warm paper.
- **Pride, Trans, Union Jack** use the screen flag palettes in the brand pack (`branding/source/themes.json`) and the Union Flag's navy `#012169` and red `#C8102E`. The flag colours appear as the scheme's `stripes` (decorative bars, the app-icon/bird treatment, the hero) and choose the primary/accent; surfaces stay neutral so profiles stay readable.
- **Violet** keeps the logo bird violet `#7228E8`, which most `-Colour` videos already use. **Coral** keeps the previous Coral current values. **Eucalyptus** is new, to give a green, age-neutral option.
- The previous Raspberry, Cobalt, Orchid and Solar schemes are retired. Their colours remain in `brand-colours.json` → `legacyVideo` so existing video renders stay valid.

A scheme never implies age, gender, sexuality or relationship intention, and choosing Pride or Trans is not an identity statement shown to anyone. Don't map colours to connection types (pink = romance, blue = friendship).

## Tokens

| Token | Use |
| --- | --- |
| bg | App/page background |
| surface | Cards, sheets, menus, inputs |
| text | Main readable foreground |
| muted | Secondary text (not a disabled opacity) |
| primary | Primary action fill and selected state |
| onPrimary | Text/icons on primary |
| accent | Decorative emphasis only: lines, dots, highlights. Not checked as text |
| soft | Tinted chip/selection/highlight fill |
| softText | Text/icons on soft |
| border | Separators and card edges (decorative; not enough alone for input boundaries) |

Each scheme also has `hero` / `onHero` (marketing background and the colour on it) and `stripes` (its signature colour bars). Keep foreground/background pairs together. **Never hard-code white on primary:** Standard dark, Lounge (both), Trans (both) and the dark modes of every other scheme use a dark or ivory `onPrimary`.

### Complete palette values

#### Standard — `standard`

Monochrome, neutral greys. The default.

| Token | Light | Dark |
| --- | --- | --- |
| bg | `#FAFAFA` | `#0A0A0A` |
| surface | `#FFFFFF` | `#161616` |
| text | `#0A0A0A` | `#F5F5F5` |
| muted | `#6B6B6B` | `#A3A3A3` |
| primary | `#0A0A0A` | `#F5F5F5` |
| onPrimary | `#FAFAFA` | `#0A0A0A` |
| accent | `#8A8A8A` | `#8A8A8A` |
| soft | `#F4F4F4` | `#1F1F1F` |
| softText | `#0A0A0A` | `#F5F5F5` |
| border | `#E5E5E5` | `#2A2A2A` |

#### Lounge — `lounge`

Navy with metallic gold and silver.

| Token | Light | Dark |
| --- | --- | --- |
| bg | `#F7F5F0` | `#172C45` |
| surface | `#FFFFFF` | `#1E3857` |
| text | `#172C45` | `#F0ECE4` |
| muted | `#5A6474` | `#C1C1C1` |
| primary | `#1E3857` | `#D0B786` |
| onPrimary | `#F0ECE4` | `#172C45` |
| accent | `#B39760` | `#C1C1C1` |
| soft | `#EFE7D6` | `#2A4566` |
| softText | `#5E4A1E` | `#E6D5AE` |
| border | `#DCD5C6` | `#3A5577` |

#### Pride — `pride`

Rainbow stripes on neutral, violet primary. Stripes: `#E40303 #FF8C00 #FFED00 #008026 #24408E #732982`.

| Token | Light | Dark |
| --- | --- | --- |
| bg | `#FBFAFC` | `#121014` |
| surface | `#FFFFFF` | `#1C1820` |
| text | `#1A1220` | `#F7F3FA` |
| muted | `#645C6B` | `#BDB3C4` |
| primary | `#732982` | `#D7A8E6` |
| onPrimary | `#FFFFFF` | `#2A0E33` |
| accent | `#FF8C00` | `#FFB347` |
| soft | `#F1E6F4` | `#2E2234` |
| softText | `#5A1F66` | `#E9CCF2` |
| border | `#E6DEE9` | `#3A3040` |

#### Trans — `trans`

Blue/pink/white flag colours. Stripes: `#5BCEFA #F5A9B8 #FFFFFF #F5A9B8 #5BCEFA`. The light-mode blue button is only 1.8:1 against the page, so it always carries its dark label and, where it sits on `bg`, a `border`-coloured outline.

| Token | Light | Dark |
| --- | --- | --- |
| bg | `#FAFCFE` | `#0D1216` |
| surface | `#FFFFFF` | `#162027` |
| text | `#0E1A22` | `#F3F8FB` |
| muted | `#56636C` | `#B4C2CB` |
| primary | `#5BCEFA` | `#5BCEFA` |
| onPrimary | `#0A1A24` | `#0A1A24` |
| accent | `#F5A9B8` | `#F5A9B8` |
| soft | `#FDECEF` | `#3A2430` |
| softText | `#8A2E45` | `#F9C9D3` |
| border | `#DCE6EC` | `#2B3A44` |

#### Union Jack — `union`

Navy/red/white flag colours. Stripes: `#012169 #FFFFFF #C8102E #FFFFFF #012169`. Dark mode moves the action colour to a lifted red so it stays visible on navy-black.

| Token | Light | Dark |
| --- | --- | --- |
| bg | `#FAFAFC` | `#0A0F1F` |
| surface | `#FFFFFF` | `#131A31` |
| text | `#0B1430` | `#F3F5FA` |
| muted | `#555D72` | `#B3BACB` |
| primary | `#012169` | `#FF6B7F` |
| onPrimary | `#FFFFFF` | `#2B0710` |
| accent | `#C8102E` | `#8EA2E8` |
| soft | `#E6EAF5` | `#1E2A52` |
| softText | `#012169` | `#C9D3FF` |
| border | `#D9DDE8` | `#2C3655` |

#### Violet — `violet`

Violet/tangerine contrast, from the logo.

| Token | Light | Dark |
| --- | --- | --- |
| bg | `#FAF8FE` | `#140D1C` |
| surface | `#FFFFFF` | `#23162F` |
| text | `#23162F` | `#F7F2FD` |
| muted | `#6E607D` | `#B9A8CC` |
| primary | `#7228E8` | `#BD87F2` |
| onPrimary | `#FFFFFF` | `#23162F` |
| accent | `#FF8C42` | `#FFAF72` |
| soft | `#EEE8FA` | `#34224A` |
| softText | `#5420C5` | `#DCC6F7` |
| border | `#E2D8ED` | `#4D3266` |

#### Coral — `coral`

Warm coral/cool blue counterpoint.

| Token | Light | Dark |
| --- | --- | --- |
| bg | `#FFF8F4` | `#211410` |
| surface | `#FFFFFF` | `#32201B` |
| text | `#35201C` | `#FFF1E8` |
| muted | `#795C52` | `#DABCAE` |
| primary | `#BE402C` | `#FF9C80` |
| onPrimary | `#FFFFFF` | `#45190F` |
| accent | `#168CBA` | `#77D4F5` |
| soft | `#FFE2D6` | `#512C23` |
| softText | `#8E2C1D` | `#FFC7B4` |
| border | `#EED9CE` | `#6C493D` |

#### Eucalyptus — `eucalyptus`

Gum green with wattle gold.

| Token | Light | Dark |
| --- | --- | --- |
| bg | `#F6F8F5` | `#0E1511` |
| surface | `#FFFFFF` | `#17221C` |
| text | `#15221B` | `#EEF5F0` |
| muted | `#56655C` | `#AEBFB4` |
| primary | `#2F6B4F` | `#8CCBA8` |
| onPrimary | `#FFFFFF` | `#0E2A1C` |
| accent | `#C9A15B` | `#E6C27A` |
| soft | `#E3EEE7` | `#203329` |
| softText | `#1F4E38` | `#BFE3CC` |
| border | `#D5DED7` | `#2E4237` |

### Fixed colours (every scheme, every mode)

These come from `brand-colours.json` and are never changed by a user's scheme.

| Purpose | Hex | Notes |
| --- | --- | --- |
| Photo verified badge | `#087CFA` | Blue tick. "Photos matched to a verification photo"; doesn't prove age. |
| ID verified badge | `#00AAA0` | Teal tick. "Government ID and age were checked". |
| Founding member | `#D4AF37` | Gold bird. |
| Swoovr staff | `#E0115F` | Ruby bird. Staff never also show founding. |
| Swoovr guide (bot) | `#C0C0C0` | Silver bird. Bots are always marked. |
| Success | `#1E7A46` / `#5FD08F` | Light / dark. Always with an icon or label. |
| Warning | `#8A5300` / `#F5B84A` | Light / dark. |
| Error | `#C0262D` / `#FF7A7F` | Light / dark. |
| Info | `#0864C8` / `#6AB0FF` | Light / dark. |

Status colours meet 4.5:1 against `bg` and `surface` in all 16 themes (checked by `build.py`).

## Accessibility

`build.py` checks **232 pairs** at 4.5:1 or better: text/bg, text/surface, muted/bg, muted/surface, onPrimary/primary and softText/soft for every scheme and mode (96), the four status colours on bg and surface in every theme (128) and onHero/hero for every scheme (8). These are solid-colour normal-text checks, not a full WCAG audit.

Not covered, and still needed in the product: accent used as text, borders as the only visible edge of an input, hover/pressed/focus/disabled states, text over photos or gradients, and non-text contrast of controls (notably Trans light's blue button on its page). Derive and test those per state; don't apply arbitrary opacity to whole controls. Honour reduced motion (no animated whole-screen colour transitions), text scaling, touch targets, focus rings and screen-reader names. Don't call the app "fully accessible" from palette checks alone.

Profile photos always stay in original colour. Text over images needs a solid backing surface.

## Preference model and persistence

Store the scheme and the requested mode separately; derive the effective mode at runtime.

```csharp
public enum AppearanceMode { System, Light, Dark }

public sealed record AppearancePreference(
    string SchemeId = "standard",
    AppearanceMode Mode = AppearanceMode.System,
    int SchemaVersion = 2);
```

- Validate scheme IDs and modes on client and server. Unknown or removed values (including the retired `electric`, `raspberry`, `cobalt`, `orchid`, `solar`) fall back to **Standard + System** without crashing or leaving a half-themed screen. Optional migration for schema v1: `electric`→`violet`, `coral`→`coral`, everything else → `standard`.
- Store enum names or explicit wire values, not numeric ordinals. Never accept arbitrary CSS or resource keys as values.
- With an account, the last successfully synced preference is the baseline across devices. Cache locally so the right theme applies before the API responds. A pending local change wins on that device while offline and syncs when back online. Resolve conflicts with a server version/ETag, not client clocks.
- Guests can choose locally; on first sign-in use the account's preference if it has one, otherwise seed it from the guest choice. Never carry one account's cached choice into another. Clear account-specific cache on sign-out.
- Suggested endpoint: `GET/PATCH /api/me/appearance`, account taken from authentication. Not part of entitlements or identity claims.

## Web (Blazor) implementation

Put the stable scheme and the **resolved** mode on the root element and read semantic custom properties:

```html
<html data-scheme="standard" data-theme="dark">
```

`tokens.css` already contains every `:root[data-scheme][data-theme]` block (plus status, hero, badge and marketing variables); generate the app's stylesheet from `themes.json` rather than hand-copying.

```css
body { background: var(--bg); color: var(--text); }
.profile-card { background: var(--surface); color: var(--text); border: 1px solid var(--border); }
.primary-action { background: var(--primary); color: var(--onPrimary); }
.intention-chip { background: var(--soft); color: var(--softText); }
```

Resolve System with `matchMedia('(prefers-color-scheme: dark)')` and listen for changes (dispose listeners). Apply the cached preference before first paint with a small CSP-compatible bootstrap so there's no wrong-theme flash. Public/SSR pages use Standard; user-specific markup must not leak through shared caches.

## MAUI implementation

A theme service resolves scheme and mode and updates `ResourceDictionary` entries on the UI thread; components use `DynamicResource`. `AppThemeBinding` alone covers mode but not the eight schemes.

```xml
<Button Text="Message"
        BackgroundColor="{DynamicResource ThemePrimary}"
        TextColor="{DynamicResource ThemeOnPrimary}" />
```

Map bg→ThemeBackground, surface→ThemeSurface, text→ThemeText, muted→ThemeMuted, primary→ThemePrimary, onPrimary→ThemeOnPrimary, accent→ThemeAccent, soft→ThemeSoft, softText→ThemeSoftText, border→ThemeBorder. Update status/navigation bar contrast where supported. Platform sign-in sheets and store purchase screens keep their own styling.

## Acceptance criteria

1. Settings → Appearance shows all eight schemes in three groups, with light/dark previews; Standard + System is the default and the reset target.
2. Every scheme works in System, Light and Dark (24 combinations, 16 resolved themes).
3. A scheme change applies instantly across discovery, profiles, chat, plans, vouchers, dialogs, menus, settings and error states.
4. Changing mode keeps the scheme; changing scheme keeps the mode. System follows OS changes; explicit Light/Dark ignores them.
5. Restart restores the cached choice without a wrong-theme flash. Sync and offline retries never overwrite a newer choice or leak between accounts.
6. Unknown/retired values fall back to Standard + System safely.
7. Badges and status colours are identical in every scheme.
8. Contrast tests cover the pairs above plus every interactive and status state used in production.
9. Appearance never affects what others see, eligibility, visibility or billing.
10. Settings work at large text sizes, narrow widths, with keyboard and screen reader.

## Handoff: replacing an older `docs/design` folder

For the AI working in the app repository (e.g. wanderconnect-brief):

1. Replace the contents of `docs/design/` with this folder. The old concept images (`explore-concept.png`, `profile-concept.png`, `screen-board.svg`, `Swoovr Swallow Logo Concept.png`, `colour-palettes.svg`) are archived in swoovr-marketing `reference/design/`; keep them only if the repo still links to them.
2. `themes.json` keeps the same shape (array of `{id, name, desc, harmony, light, dark}` with the same ten tokens) and adds `group`, `hero`, `onHero` and `stripes`. Update `scripts/check_design.py`: the ID list is now `['standard', 'lounge', 'pride', 'trans', 'union', 'violet', 'coral', 'eucalyptus']`. Its studio regex (`const palettes=…;\nlet selected`) and doc-table format still match these files.
3. Change the default scheme from `electric` to `standard` everywhere (preference record, fallbacks, tests, generated catalogue), add the v1→v2 migration above, and regenerate `themes.css` / `appearance-palettes.js` / `AppearanceSchemes.g.cs`.
4. Update `AGENTS.md`: "Theme values originate in docs/design/themes.json (copied from swoovr-marketing `design/`). Standard black and white is the default; eight personal schemes are user choices. Never turn them into one global branding choice."
5. Treat this folder as read-only in the app repo. Colour changes are made in swoovr-marketing and copied across.
