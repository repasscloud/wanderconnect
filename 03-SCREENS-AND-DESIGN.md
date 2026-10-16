# Screens and design system

## 1. Brand direction

Warm, clear, adult, inclusive and approachable. Avoid casino-like rewards, nightclub-only styling, childish hearts everywhere or sexualised marketing. Not a promise to serve literally every unrelated app category.

Three proposed palettes; none approved. Existing teal images are concept references, not locked branding. Use semantic design tokens so themes can change without rewriting screens. Validate actual foreground/background combinations and interaction states for accessibility before release.

| Token | A: Teal & ivory | B: Indigo & apricot | C: Ink & terracotta |
| --- | --- | --- | --- |
| Background | #FAF8F4 | #F8F8FC | #FBF7F2 |
| Surface | #FFFFFF | #FFFFFF | #FFFFFF |
| Text | #17252B | #202238 | #252932 |
| Primary | #075B5E | #4545A3 | #944632 |
| Soft primary | #E2F0EB | #ECEBFA | #F6E8E0 |
| Accent | #BF6245 | #B94F32 | #315E68 |
| Border | #D8DFDA | #DDDDE8 | #E2D9D1 |

DEFAULT A retains continuity with existing mockups. B offers a stronger alternative if teal feels too wellness-oriented. C feels earthy and social. Dark-mode baseline: background #141A20, surface #202831, text #F4F5F7; derive accessible brand accents rather than blindly reusing light-mode tokens. Error, warning, success and focus colours are semantic, never the only indicator.

Use a readable system sans-serif for controls/body; optional restrained serif for large editorial headings as in reference images. Body target 16 logical pixels, comfortable line height; support dynamic text. 8-unit spacing system, 12–16 corner radius, restrained shadows. Minimum target sizes follow each platform's guidance; target at least 44 logical points for mobile actions. Do not compress text into unreadable two-column cards at large accessibility sizes: switch to a one-column list.

## 2. Navigation and responsive layout

Mobile bottom tabs: Explore, Connections, Chats, Me. Explore switches People / Plans. Location and filters remain accessible; incognito has explicit current state. Desktop uses persistent navigation, filter sidebar, card grid and optional profile side panel. Preserve back position and scroll. Stable pagination/session ordering: no involuntary reshuffle while reading.

## 3. Screen inventory

| Screen | Layout and behaviour | Critical states |
| --- | --- | --- |
| Welcome | Neutral connection imagery, short purpose, Apple/Google/email buttons | Demo auth explicitly labelled; errors; accessible provider branding |
| Onboarding | Age gate, terms, intentions, identity check, profile, visibility; save progress | Underage stop; verification pending/fail; photo skipped |
| Intentions | General/current context, timing, explanation of each label | Conflicting filter rule; disclosure preview |
| Explore People | Location, incognito indicator, filter chips, two-column cards | Loading, empty, offline, permissions denied, all profiles filtered |
| Filters | Required ANY/ALL, excluded, contact preferences, age/distance, sparse/visitor controls | Reset; explicit Apply; preview without leaking excluded users |
| Profile detail | Gallery OR substantive text hero, name/age, verification, approximate area, intentions, bio, prompts, interests, optional travel, actions | Sparse; messaging restricted; visitor awaiting arrival; blocked/deleted |
| Profile editor | Display name, bio, prompts, photos, intentions, social links, previews | Substance checklist; upload pending; duplicate-image review |
| Connections | Received/sent interest, mutual, saved; discreet counts | Declined request; eligibility changed; no repeated notification |
| Inbox | Accepted chats separate from requests, search, mute settings | Spam request; blocked sender; unread privacy controls |
| Chat | Text, accepted media, plan card, report/block, contextual profile | Pending request; failed/retry; offline; intention changed |
| Plans | Own upcoming plans and discoverable invitations with context/date | Expired invitation; full/cancelled; timezone change |
| Plan composer | Who, intention, activity, date/time, venue, cost split | Counterproposal; unavailable venue; location privacy |
| Destination | Place search, visit dates, publish toggle, current/upcoming visitors | Not physically there; no location permission |
| Venue detail | Accessible venue description, real photos, availability, inclusions, price/terms | Sold out; dietary/accessibility information unknown |
| Checkout | Itemised real-world experience, attendees, payer, policy, processor | Payment pending/fail; reservation expired; refund |
| Voucher wallet | Upcoming/used vouchers with venue, schedule, terms | Pending booking; invalid/expired/redeemed |
| Voucher detail | QR/code, redemption status, support and cancellation | No reusable static value token; offline handling explained |
| Feedback | Private structured questions and separate safety report | Did not meet; skipped; submitted; report confirmation |
| Privacy/settings | Incognito, owner acceptance, visibility, blocked list, sessions, notification privacy | Consistent policy preview and save errors |
| Account/billing | Entitlements, payment source, manage/restore, deletion/export | Renewal/refund/grace; provider management destination |
| Venue portal | Bookings, offers, redemption scanner, staff permissions | Double redemption; staff revoked; audit history |
| Admin portal | Reports, verification exceptions, image matches, appeals, venue operations | Least-privilege access, evidence trails, case assignment |

## 4. Card and profile design rules

Do not treat the sample profile text as production copy. Cards display photo when supplied, name/age where permitted, brief meaningful text, applicable intentions, approximate area and appropriately worded verification. A photo-free card is intentionally typographic, not a giant silhouette. Optional visitor label must not be mistaken for current presence.

Profile detail's sticky primary action adapts to owner policy: Message, Request to chat, Continue conversation or unavailable with explanation. Express interest can be secondary. No public ratings or popularity counters. An overlap panel reports explicit shared choices only, not a made-up compatibility score. No legal name or ID document display.

## 5. Social link UI

Optional verified-ownership or self-declared badges must differ. Provide an explicit warning that an external link may reveal other information. Let the owner choose link audience. Do not include links on discovery cards by default. Blocking stops future platform disclosure but cannot erase information already shared externally.

## 6. Required design deliverables for implementation

Mobile and desktop layouts for all core screens; loading/empty/error/denied states; light/dark tokens; typography; icons with accessible labels; keyboard focus and screen-reader reading order; reduced motion; large text; image descriptions; privacy-safe notifications. The SVG board is a concept layout, not final pixel-perfect production UI. The two PNGs preserve the prior conversation's visual concepts.
