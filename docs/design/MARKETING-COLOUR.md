# Marketing colour

How every Swoovr post, video, image and web page uses colour. The app schemes are in `USER-APPEARANCE-THEMES.md`; the exact values are in `themes.json` and `brand-colours.json`; open `swoovr-colour-studio.html` to see and copy them.

## The order

1. **Black and white first.** Every video's main version and every post image is ink on white, Helvetica Neue, clean and Runway-like. That is the brand.
2. **Colour second.** A video's `-Colour` (and `-TikTok` / `-Shorts`) version swaps the ink background for **one scheme's hero colour**. Everything on top stays white or ink; no extra colours appear.
3. **Colour is a user choice.** When copy talks about colour, say it's chosen in Settings → Appearance in a planned design ("Standard black and white, or pick one of eight schemes"). Never imply a scheme says anything about who someone is.

## Core neutrals

| Name | Hex | Use |
| --- | --- | --- |
| Ink | `#0A0A0A` | Text, buttons, the main video background |
| Muted | `#6B6B6B` | Secondary text, captions on white |
| Line | `#E5E5E5` | Borders, dividers |
| Soft | `#F4F4F4` | Photo-free tiles, chips |
| Off-white | `#FAFAFA` | Page/canvas behind white cards |
| White | `#FFFFFF` | Cards, text on dark |

Carbon `#0D0F12` and paper `#F6F4EF` (usefulstash) are the approved warmer off-black/off-white if a piece needs a softer, editorial feel. Don't mix them with ink/off-white in the same piece.

## Hero backgrounds (the `-Colour` versions)

| Scheme | Hero | On hero | Good for |
| --- | --- | --- | --- |
| Standard | `#0A0A0A` | `#FAFAFA` | Anything; the dark main version |
| Lounge | `#1E3857` | `#D0B786` gold | Founding members, referrals, premium/VIP, evening plans |
| Pride | `#23162F` + rainbow stripes | `#FFFFFF` | Pride month, Feast Festival, inclusive features |
| Trans | `#5BCEFA` | `#0A1A24` ink | Trans Day of Visibility (31 Mar), Remembrance (20 Nov), inclusive features |
| Union Jack | `#012169` | `#FFFFFF` | UK expats, travel, cheeky/British humour |
| Violet | `#7228E8` | `#FFFFFF` | General brand videos, the logo colour |
| Coral | `#BE402C` | `#FFFFFF` | Experiences, summer, beach, social |
| Eucalyptus | `#2F6B4F` | `#FFFFFF` | Outdoors, friendship, activities, older audiences |

Rules for heroes:

- Text and UI on a hero use only its `onHero` colour (and white phone screens). Trans is the only light hero: use ink text on it.
- Pride's rainbow appears as stripes (a bar, a band behind the phone, or the bird fill), never as a full-screen rainbow under text.
- Flag stripes keep their order and proportions; don't recolour or distort them. Flags don't imply any organisation endorses Swoovr.
- Don't put two heroes in one piece. Rotate them across the calendar.

## VIP metals

From swoovr.com/referral: navy `#1E3857`, navy deep `#172C45`, gold `#D0B786`, silver `#C1C1C1`, graphite `#454545`, ivory `#F0ECE4`. Use them for founding and referral material, gold for headlines on navy, silver for supporting text, ivory for body copy. Don't call anything "founding-member benefits" (claims rules).

## Badges (never change)

Photo verified blue `#087CFA`, ID verified teal `#00AAA0`, founding gold bird `#D4AF37`, staff ruby bird `#E0115F`, bot silver bird `#C0C0C0`. Same in every scheme, video and post.

## Existing videos

Current `-Colour` videos use `THEME` in `video/src/Root.tsx`: violet `#7228E8`, cobalt `#2454D6`, raspberry `#BC1459`, coral `#BE402C`, orchid `#9430BC`. Those renders stay valid. For new or re-rendered videos, pick a hero from the table above:

| Old | Use instead |
| --- | --- |
| violet `#7228E8` | Violet (same colour) |
| coral `#BE402C` | Coral (same colour) |
| cobalt `#2454D6` | Union Jack or Lounge |
| raspberry `#BC1459` | Coral or Pride |
| orchid `#9430BC` | Violet or Pride |
| solar `#F4C52F` | Retired |

`design/tokens.ts` exports `HERO` (scheme id → hex) and `THEMES` for the Remotion project to import.

## Changing a colour

Edit `themes.json` or `brand-colours.json`, then run `python3 design/build.py`. It refuses to write if any required pair drops below 4.5:1, and regenerates the studio, the spec tables, `tokens.css`, `tokens.ts` and the swatch. `python3 design/build.py --check` only checks (for CI or before committing).
