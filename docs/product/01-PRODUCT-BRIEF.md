# Swoovr product brief

Updated 22 October 2026: Swoovr is the owner-confirmed product name. See [current status](../delivery/STATUS.md) for implementation progress and [naming](../architecture/NAMING.md) for engineering conventions.

## 1. Vision

Build a platform where adults discover real people nearby or abroad for the connections both actually want: friendship, conversation, activities, dating, romance, long-term relationships, casual connections and friends with benefits. Add optional organised venue experiences with vouchers, without making paid dates the compulsory path.

The product must support different adult life stages without assigning motivations by age, gender, orientation or appearance. An adult looking for a spouse, a traveller seeking company and a local seeking a casual connection should each receive a relevant experience with explicit boundaries.

Working positioning: **Real people. Clear intentions. Your kind of connection.** This is proposed copy, not approved brand language or a guarantee that all accounts are genuine.

## 2. Confirmed owner requirements

- All adult genders and orientations; broad forms of connection rather than a narrow dating or travel-only audience.
- Grid-based discovery with informative profile cards; no compulsory one-person swipe deck.
- Require and exclude intentions independently. A shared LTR intention does not override an excluded FWB or casual intention.
- Owner-controlled visibility and interaction eligibility.
- Incognito browsing leaves no profile-view trace visible to the visited member. Members can exclude incognito visitors entirely.
- Photos are optional. “Blank” means sparse substance, not missing a picture. Sparse status is communicated to the account owner; other members can filter sparse profiles out.
- Identity verification through ID or an appropriate alternative; image-duplication detection as an additional fraud signal.
- Private post-interaction/date feedback to improve matching and identify concerning patterns.
- Local discovery and travel visibility, with travellers unable to initiate contact with destination locals until physically present in the eligible area.
- Partner venues and voucher-based dates/meetings.
- Apple and Google sign-in, optional social account links, a website and iOS/Android clients.
- Central account entitlements and backend-enforced access.

## 3. User jobs

| User situation | Job | Desired result |
| --- | --- | --- |
| Relationship seeker | Exclude intentions that conflict with mine | Fewer irrelevant approaches |
| Friendship seeker | Meet someone who actually wants new friends | Platonic connection without presumed romantic escalation |
| Casual connection seeker | Find reciprocally interested adults | Clear expectations without intruding on other audiences |
| Traveller | Announce an upcoming visit and connect on arrival | Meet locals or travellers without pretending to be local |
| Private member | Browse without public visit history | Privacy within the other person's access rules |
| Photo-free member | Present a meaningful written profile | Participate without a mandatory public portrait |
| Venue partner | Offer a defined meeting experience | Controlled bookings and auditable voucher redemption |

## 4. Differentiation hypotheses

The proposed advantage is the combination of explicit exclusions, accountable profiles, respectful privacy, context-aware discovery and useful real-world meeting tools. These are hypotheses to test, not proven market superiority. Do not rely on public popularity, addictive swiping, artificial scarcity or paid visibility suppression as the business model.

## 5. Product structure

One account and one core profile. Connection modes shape discovery: Dating, Friends, Activities and Business, several at once; Travel is a separate discovery state across them (D92, [profile flags and modes](08-PROFILE-FLAGS-AND-MODES.md#modes)). The earlier context labels Connect, Friends & company and Travel & exploring are legacy names, not modes. Do not expose sensitive romantic/sexual intentions in a platonic mode (Friends, Activities, Business) without the member's explicit visibility choice. A mode switch or Travel is not a second identity and cannot bypass a block.

Primary navigation: Explore, Connections, Chats, Me. Within Explore: People and Plans. Venue experiences appear within Plans and the plan composer rather than becoming an unrelated shopping feed. On the web, retain the same labels and concepts.

## 6. Scope and sequencing

### Phase A — reviewable prototype

Seeded fictional adult profiles; onboarding and simulated Apple/Google/email login; profile editor; profile-substance status; People grid; profile detail; required/excluded intention filters; incognito and profile eligibility; local/destination discovery; travel states; interests and messaging; invitations; partner venue booking and voucher lifecycle; feedback; account settings; admin review and venue portal examples. Demonstrate both allowed and denied actions. Payments and identity checks use labelled sandbox/demo states.

### Phase B — closed pilot

Real authentication, identity provider, private media storage, notifications, chat, moderation operations, device location eligibility, provider-backed purchases and webhook processing, selected contracted venues, redemption tools, deletion/export flows and operational monitoring. Start in a compact geographic community; do not represent a pilot as global coverage.

### Phase C — expansion

Additional cities/countries, languages and currencies; evidence-based ranking improvements; more venue inventory; optional calendar integrations; expanded experience formats. Do not turn low-data prototype heuristics into claims of AI compatibility.

## 7. Measures

Track eligible active members per local reciprocal-preference pool; discovery-to-interest; interest-to-conversation; mutually confirmed meetings; repeat wanted connections; cancellations/no-shows; safety report response; verification completion; sparse-profile improvement; venue redemption success; false fraud flags; incognito/filter enforcement failures. Segment carefully without exposing sensitive small cohorts.

Success includes friendship, meaningful conversation and ongoing relationships, not just dates or app engagement. Do not optimise solely for time spent or number of approaches.
