# Swoovr UI style: what every screen must look like

Status: confirmed by the owner, 22 October 2026. This is the **look** of the app. Colours come from `../themes.json` (Standard by default); this file covers type, layout, components and motion. Where this file and older wireframes or concept images disagree, **this file wins**. Wireframes are for screen content and flow only.

## Reference files (in this folder)

| File | What it shows |
| --- | --- |
| `login.html` | **Working prototype** of launch → splash → sign-in, create account, forgot/reset password, passkey, signed in. Open it in a browser. Port its CSS and timings exactly. |
| `SignInDemo.mp4` | The same flow as a 29 s video. Watch the first 7 seconds for the launch animation. |
| `screens/00-contact-sheet.jpg` | Every reference screen on one sheet. |
| `screens/01–04` | Splash: bird drawing, logo + wordmark, white sheet wiping up, finished sign-in. |
| `screens/05–06` | Forgot password, create account. |
| `screens/07–09, 12` | Explore grid, Friends tab, filters sheet, mixed photo/photo-free tiles. |
| `screens/10–11` | Profile with badges, profile with no public photo. |
| `screens/13–14` | Plan a date, chat. |
| `source/ui.tsx`, `source/SignInDemo.tsx` | The React components the screens were drawn with (inline styles in iPhone points). Use them for exact numbers. |

In swoovr-marketing this folder keeps only `UI-STYLE.md`, `login.html` and the contact sheet; the full frames, `SignInDemo.mp4` and `source/` are copied from `video/out/` and `video/src/` when the folder is handed to the app repo. The screenshots are 1080×1920 video frames with a caption on top and a phone in the middle; only the phone screen is the UI. People in them are **design concept · illustrative profiles**, not members.

## Feel

Black and white, ultra modern, professional, clean and crisp (think Runway, Apple, Linear). Lots of white space, one strong black action per screen, hairline borders, no shadows on cards, no gradients, no coloured panels, no illustrations, no emoji. Photos and the small verification badges are the only colour in Standard.

**Not this:** the colour-heavy wireframe/concept look, Bootstrap/Material defaults, PatternFly defaults, system-blue links, drop-shadowed cards, rounded pill buttons in brand purple, Inter/Roboto/Segoe UI.

## Type

One family: **`"Helvetica Neue", Helvetica, Arial, sans-serif`** everywhere, with `-webkit-font-smoothing: antialiased`. No web font downloads, no Inter, no system-ui fallback first. The one exception: the quote on a photo-free profile tile is **Georgia** serif (`Georgia, "Times New Roman", serif`).

Sizes in points/CSS px at phone width (390):

| Role | Size / weight | Notes |
| --- | --- | --- |
| Page title ("Welcome back.", "Create your account.", "Forgot password?") | 32 / 700, line-height 1.05, letter-spacing −1px | Full stop on sign-in titles. |
| Screen title ("Explore", "Plan a date") | 26–30 / 700, letter-spacing −0.5px | Left aligned, no centred nav titles. |
| Section heading ("Looking for", "About me", "Filters") | 15–17 / 700 | |
| Name on profile | 22–28 / 700 | Badges sit right after the name. |
| Body | 15 / 400, line-height 1.4 | |
| Subtitle under a title | 15 / 400, muted | |
| Tile name | 15 / 700 | "Erin, 36" |
| Field label | 11 / 400, UPPER CASE, letter-spacing .3px, muted | Sits inside the input, top-left. |
| Chip | 11–12 / 600 | |
| Tab bar label | 11 / 400 (700 when active) | |
| Divider text ("OR") | 12, UPPER CASE, letter-spacing .4px, muted | |
| Footer claims line | 11, muted; version line 10.5 monospace `#9A9A9A` | "In development · Starting in Adelaide · Adults 18+" |
| Wordmark on splash | 30 / 700, letter-spacing 9px, lower case "swoovr" | |

## Layout

- Page side padding 24px; content stacks with 10px gaps inside forms, 26px above a form.
- Screen title starts ~30px below the top bar. One primary action per screen, full width.
- Bottom tab bar: Explore, Connections, Chats, Me. White, 1px top border `#E5E5E5`, 80px tall, outline icons, active item ink + bold, others muted.
- Profile grid: two columns, 10px gap, **every tile exactly the same size** (170×270 at 390 wide), photo or not. Photo tiles: photo top 170px, then name + badges, chips, area with pin icon. Photo-free tiles: ink background, white Georgia quote, name and intentions at the bottom.
- Profile page: large photo with 16px corners (or the ink quote block if no public photo), name + badges, area, intention chips, then cards for "Photo verified / ID verified" with checked dates, "About me", "We could…", "Interests". Actions pinned at the bottom: outline "Express interest" + ink "Request to chat".
- Sheets (filters): white, 24px top corners, grab handle, dimmed backdrop, full-width ink "Apply".
- Desktop/web: same components, centred column max 420px for sign-in and forms, max ~1100px for grids (more columns, same tile size). Never stretch tiles.

## Components

| Component | Spec |
| --- | --- |
| Input | 56px tall, 1px `--border`, radius 14px, `--surface` fill, label inside (11px upper case), text 16px, placeholder `#B5B5B5`. Focus: border `--text` + 3px ring `rgba(10,10,10,.06)`. Error: message 12px below. |
| Primary button | 54px, radius 14px, `--primary` fill, `--onPrimary` text 16/600, full width. Pressed: scale .98. Disabled: `#D4D4D4`, no pointer. Loading: label replaced by a small spinner. |
| Secondary button | Same size, `--surface` fill, 1px `--border`, `--text` label; hover border `--text`. |
| Apple / Google / passkey | Apple: ink fill, white Apple mark. Google: outline, Google's colour "G". Passkey: outline with key icon. Follow each provider's brand rules. |
| Link | `--text`, 13px, 1px `--border` underline that turns `--text` on hover. Never blue. |
| Checkbox | 24px, radius 7px, 1.5px `#BDBDBD` border; checked: ink fill, white tick, tiny scale pop. |
| Chip | Radius 20px, padding 4×9. Selected: ink fill, white text. Unselected: white, 1px border. "Ruled out": muted text. |
| Segmented control (Dating / Friends / Activities) | `--soft` track, white selected segment, 13/600. |
| Card | `--surface`, 1px `--border`, radius 14–16px, **no shadow**. |
| Badges | 16px icons right after the name: photo verified blue `#087CFA`, ID verified teal `#00AAA0`, founding gold bird, staff ruby bird, bot silver bird. Same in every scheme. |
| Chat | Their bubble: white, 1px border, bottom-left corner 4px. Mine: ink, white text, bottom-right corner 4px. Composer: soft pill input + round ink send button. |
| Icons | Thin outline (1.5px stroke), ink or muted. One icon set throughout. |

## Launch → splash → sign-in (the owner's favourite; build it exactly)

Port the `.splash` markup, CSS and timings from `login.html`. Sequence on an ink `#0A0A0A` screen:

| Time | What happens | CSS in `login.html` |
| --- | --- | --- |
| 0.15–1.45 s | The bird draws itself as a 3px white outline (`stroke-dasharray: 1` with `pathLength`, dash offset 1 → 0). | `draw 1.3s cubic-bezier(.76,0,.24,1) .15s` |
| 0.9–2.4 s | A 120×1px hairline under the logo fills left to right (progress feel). | `load 1.5s … .9s` |
| 1.25–1.75 s | The outline fills solid white. | `fill .5s ease 1.25s` |
| from 1.2 s | "swoovr" rises letter by letter out of a mask, 55 ms apart. | `rise .7s cubic-bezier(.2,.8,.2,1)` + `--i` delay |
| 1.5 s | "Dating · Friendship · Activities" fades in near the bottom, 55% white. | `fadeIn .6s ease 1.5s` |
| 2.75–3.65 s | The ink splash wipes **up** off the screen with rounded bottom corners (48px), revealing the white sign-in page underneath. The status bar turns from white to ink. | `wipe .9s cubic-bezier(.76,0,.24,1) 2.75s` (`clip-path: inset(0 0 100% 0 round 0 0 48px 48px)`) |
| after the wipe | The sign-in elements stagger in (logo row, title, subtitle, fields, buttons), each fading/rising slightly, ~40–60 ms apart. | `.in` with `--d` delays |

Rules:

- Show it on a cold start of the app and on the first load of the web sign-in page in a session. Don't replay it on every navigation. The web can use a shorter version (about 2.2 s) but keep the same steps.
- Honour `prefers-reduced-motion`: show the filled logo for ~600 ms, then a plain cross-fade to sign-in.
- Screen-to-screen inside sign-in (sign in ↔ create account ↔ forgot) slides horizontally like iOS push: new screen in from the right, a slight shadow on its leading edge, back slides it out.
- Native (MAUI): match the timings with the platform splash showing the static ink background + bird, then the animated sequence as the first page, so there's no white flash between them.
- Keycloak hosted pages can't run the full splash between redirects. Run the splash in the app before handing off to Keycloak, and keep the Keycloak pages visually identical to the prototype's sign-in screen (see the Keycloak theme), so the switch is invisible.

## Copy on these screens

As in the prototype: "Welcome back." / "Sign in to dating, friendship and things to do." / "Create your account." / "Swoovr is in development, starting in Adelaide." / "I'm 18 or over" (required, blocks submit) / "Forgot password?" / "Check your email." / "Choose a new password." Footer on every pre-sign-in screen: "In development · Starting in Adelaide · Adults 18+". Never: safe, trusted, background checked, download, live.

## Checklist before calling a screen done

1. Helvetica Neue everywhere (check the computed font, not the CSS file).
2. Only tokens from `themes.json`; no stray hex values, no framework default colours.
3. Compare it side by side with the matching screenshot here at 390 wide: type sizes, weights, spacing, radii, borders.
4. Check it in Standard light **and** dark, then one colour scheme.
5. All profile tiles the same size; badges after names; mockup people labelled as illustrative where shown in marketing.
