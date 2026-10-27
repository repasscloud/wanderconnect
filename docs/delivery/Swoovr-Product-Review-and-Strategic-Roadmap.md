# Swoovr — Product Review & Strategic Roadmap

**Product assessment:** 26 October 2026  
**Scope:** Dating / Friends / Activities / Business

## Executive summary

**Swoovr has the foundations of a significantly more capable platform than a conventional dating app.** The opportunity is not simply to compete with Tinder, Grindr or Bumble. It is to build a platform that helps people discover compatible individuals, form genuine connections and turn those connections into real-world experiences.

After reviewing our previous Swoovr discussions and researching the current market, I see three important conclusions.

1. **The product direction is strong.** Independent modes, meaningful verification, explainable compatibility, travel discovery, venue partnerships and integrated planning are complementary features with a coherent purpose.
2. **Several important product systems are still missing or insufficiently defined.** Particularly groups, events, connection lifecycle, business identity, trust and safety operations, user retention, privacy boundaries between modes, and the complete experience after two people connect.
3. **The biggest risk is attempting to launch too many interconnected products simultaneously.** Dating, friendship, activities, professional networking and venue commerce each have their own network effects. They need a shared foundation, but should not all require complete feature parity at launch.

The market also supports the direction of moving beyond conventional swiping. Adjust reported a 4% decline in dating-app installs and a 7% decline in sessions during 2025, while a 2026 Australian survey commissioned by Coffee Meets Bagel identified ghosting, fatigue and shallow profiles as major frustrations. These are industry and survey findings, not forecasts of Swoovr's performance.

## Overall assessment

| Area | Assessment | Qualitative planning completeness |
|---|---|---:|
| Product vision and differentiation | Strong | 90% |
| Discovery and profile concepts | Strong | 82% |
| Verification and safety design | Needs completion | 68% |
| Friendship, activities and communities | Underdeveloped | 43% |
| Business networking | Underdeveloped | 32% |
| Monetisation architecture | Promising | 70% |
| Launch readiness | Significant work | 38% |

> These figures are qualitative assessments of the completeness of product planning, **not** measured engineering progress or independently verified implementation status.

## 1. What already works

These are concepts I would preserve rather than redesign.

| Existing decision | Why it matters |
|---|---|
| Four independent modes: Dating, Friends, Activities and Business | People can maintain multiple intentions without forcing every interaction into dating. |
| Travel as an independent state | Discovery works before and during travel without becoming an unrelated fifth mode. |
| Photo Verified and ID Verified as separate credentials | More transparent than one ambiguous verified tick. |
| Anonymous or discreet profiles with identity assurance | Supports people who need privacy without treating anonymity as automatic dishonesty. |
| Contextual Weighted Compatibility Algorithm | Allows compatibility to depend on the relationship context and individual priorities. |
| Grid-based discovery rather than endless swiping | Encourages intentional exploration and comparison. |
| Venue discovery, date planning and booking | Provides a practical reason to use Swoovr beyond browsing profiles. |
| Founder programme with 1,000 places | Creates early community identity and measurable launch incentives. |
| Adelaide-first launch | Allows concentration of members, venues and support operations before expansion. |
| Swoovr Sponsored alongside AdMob | Combines local commercial value with scalable advertising, while keeping sponsorship separate from personal compatibility. |

Two especially important architectural decisions are worth preserving: **verification is not equivalent to trustworthiness**, and **compatibility should never automatically exclude someone merely because a numeric score is low**. Those principles should remain consistent throughout the application.

## 2. The biggest missing features

I've identified 18 areas that deserve consideration. Some are missing capabilities, while others are existing concepts that need a complete specification.

### 2.1 Groups and communities

**Priority:** High  
**Roadmap:** Basic groups in the first major expansion; richer communities later.

Swoovr currently focuses heavily on person-to-person interactions. It needs a way for people to connect through shared interests without first selecting individual profiles.

Examples include Adelaide hiking groups, LGBTQ+ social groups, singles over 40, photography enthusiasts, newcomers to Adelaide and young professional networks.

Proposed capabilities: public/private groups, memberships, group discussions, group administrators, group events and moderated invitations.

### 2.2 Events, meetups and group activities

**Priority:** High

This is probably the largest functional gap in Activities mode.

People should be able to create or join experiences such as coffee gatherings, beach walks, dinner groups, board-game evenings, business networking sessions and singles events.

It needs RSVP handling, attendance limits, waitlists, invitations, venue details, event chats, cancellation rules and organiser controls.

**Important distinction:** Discovering an activity is not the same as joining an organised event. Swoovr should support both.

### 2.3 Separate personal and professional identities

**Priority:** High

Business mode raises a privacy problem that dating-only apps do not have.

Someone may want employers, clients and colleagues to see their professional details without seeing their dating intentions, private photos or personal relationships.

I would introduce mode-specific profile views and explicit cross-mode privacy controls, backed by server-side authorisation.

A member could show their profession and business website publicly in Business mode while revealing none of their dating details to professional contacts. The system must also protect against indirect leakage through shared photos, identifiers, suggestions and notifications.

### 2.4 Connection lifecycle management

**Priority:** High

We have spent considerable time discussing profile discovery and messaging, but less on what happens when two people establish a connection.

Swoovr needs defined states for requests, accepted connections, conversations, completed meetings, cancellations, disconnections and blocked relationships.

The lifecycle should vary by mode. A friend connection should not behave exactly like a romantic match or a professional contact.

This also creates a foundation for meaningful progress metrics instead of counting likes and swipes.

### 2.5 In-app voice and video calls

**Priority:** Medium

Voice and video calls help people establish confidence without disclosing their personal telephone numbers.

Consider mutual permission, call requests, mute/block/report actions and restrictions for unverified accounts.

No automatic recording. If abuse is reported, only deliberately retained, consented-to evidence should be available to moderators.

This is especially useful for Dating and Business modes.

### 2.6 A complete meeting-safety system

**Priority:** High

Your planned privacy check-in and mutual QR confirmation are good foundations.

Extend them into a complete voluntary safety flow: share date plans with a trusted contact, optional check-in prompts, manually trigger emergency assistance, and report incidents after a meeting.

Trusted contacts should not automatically receive private chat contents or the other person's private information.

Never market these controls as guarantees of physical safety.

### 2.7 Messaging quality and conversation management

**Priority:** High

The MAUI chat interface is only one part of messaging. The underlying product needs message requests, recipient preferences, chat expiration or archiving, delivery states, notification controls, unwanted-message filtering and clear handling of disconnected users.

Conversation starters based on shared interests could help people get past empty greetings.

Add an explicit way to end a conversation politely without requiring a user to block someone.

### 2.8 Genuine business accounts and organisations

**Priority:** High

We have discussed Business mode for individuals and sponsored venue accounts, but those are not the same thing.

Swoovr needs a distinction between an individual professional, a represented organisation, a partner venue and a commercial advertiser.

Organisations could have verified business profiles, authorised representatives, enquiry controls and separate administrative access.

A professional member's business profile must not be treated as automatically verified because their personal identity is verified.

### 2.9 Better discovery when the local network is small

**Priority:** High

An Adelaide-first platform must work even when only a limited number of relevant members are nearby.

Introduce discovery explanations, recently active filters where permitted, broader radius suggestions, compatible members outside the immediate area, and future-travel discovery.

Avoid presenting inactive accounts as available just to make the application look busy.

The algorithm should optimise for useful, reciprocal connections rather than only time spent scrolling.

### 2.10 Explainable compatibility and preference management

**Priority:** Medium

The proposed compatibility algorithm needs confidence levels, visibility controls and explanations.

A 92% score based on four answered questions should not appear equivalent to a 92% score based on 120 questions.

Consider distinguishing shared interests, compatible values, lifestyle alignment, possible differences and insufficient information.

Question answers need versioning so that changing a question's meaning does not silently change historical results.

### 2.11 A dedicated Places and Experiences system

**Priority:** Medium

Venue discovery should eventually become more than a search box attached to Plan a Date.

Introduce saved places, accessible venue attributes, opening hours, price ranges, activity types, venue amenities and partner benefits.

Support places independently of their sponsorship status. A non-paying venue should still be discoverable when it is relevant.

Sponsorship should be clearly identified and never change a person's compatibility score.

### 2.12 Better recurring social interaction

**Priority:** Medium

Many friendships grow through repeated activities rather than a single meeting.

Support recurring groups and event series, such as Wednesday trivia, Sunday walking groups and monthly business breakfasts.

Members should be able to follow an organiser or subscribe to an activity series without exposing their wider social graph.

### 2.13 Reputation and positive conduct signals

**Priority:** Research first

Consider limited, non-public signals about respectful participation, confirmed event attendance and repeated misconduct.

Avoid public star ratings for individual people or a universal trust score. These could invite retaliation, discriminatory judgements and attempts to game the system.

Verification, behaviour, account standing and professional credentials should remain separate concepts.

### 2.14 Accessibility and inclusive interaction design

**Priority:** High

Define accessibility as a release requirement, not a later enhancement.

Include screen-reader labels, Dynamic Type, keyboard navigation on web, high contrast, reduced motion, accessible QR alternatives, captions and practical alternatives to complex gestures.

Make disability and access needs optional profile information with granular visibility, not assumptions embedded in matching logic.

### 2.15 Notification and privacy controls

**Priority:** High

Notifications can accidentally expose dating or personal information to someone viewing a member's lock screen.

Support per-mode notification settings, private notification previews, quiet hours, message-request notifications and travel reminders.

Incognito and mode separation must also apply to push notifications, email communications and deep links.

### 2.16 Account standing, appeals and support transparency

**Priority:** High

You have discussed human moderation and appeals, but members need a complete self-service experience.

Show verification status, account restrictions, relevant moderation decisions, appeal availability, support case references and whether action is required.

Never expose reporter identities or sensitive internal fraud signals.

### 2.17 Product analytics and experimentation

**Priority:** High

We need a measurement framework before beta, not after launch.

Track the complete member journey: signup, profile completion, discovery, connection requests, accepted connections, conversations, planned meetings and reported outcomes.

Use privacy-conscious aggregate measures and avoid treating intimate user details as advertising data.

### 2.18 Commercial operations and transaction handling

**Priority:** Medium

Storing payment methods through Stripe is only part of a complete commerce system.

Define subscriptions, entitlement changes, refunds, chargebacks, venue settlements, cancellation handling, partner reporting and financial reconciliation.

Keep paid Swoovr features distinct from physical venue services when selecting payment methods.

## 3. Improvements by application mode

### Dating

**Core launch experience**

Strengthen the progression from seeing a profile to arranging a date. Prioritise intentions, contact consent, dealbreakers, conversation quality, compatibility explanations and meeting safety.

A potential differentiator is a visible **Plan a Date** action built directly into an accepted conversation.

### Friends

**Early expansion**

Friendship needs friend requests, shared hobbies, local communities and recurring meetups. Unlike dating, friends should not be subject to artificial romantic-matching mechanics.

Make it easy to find someone for coffee, exercise, gaming or simply exploring a new city.

### Activities

**Major growth opportunity**

Activities should be usable even if someone is not actively dating or looking for a new best friend.

Centre it on events, meetups, venues and experiences, with filters such as tonight, this weekend, free, nearby, accessible and beginner-friendly.

This mode could become Swoovr's strongest regular-engagement feature.

### Business

**Separate product requirements**

Business mode needs a clearer purpose than a professional version of Dating.

Potential core uses are local introductions, collaboration, mentoring, business networking events and professional referrals.

It should have professional credentials, company affiliations, appropriate visibility controls and restrictions against unsolicited mass prospecting.

### A useful product distinction

I would define these four interactions explicitly:

| Mode | Primary connection | Success outcome |
|---|---|---|
| Dating | Two people exploring romantic or casual compatibility | A mutually wanted conversation, date or relationship |
| Friends | People building platonic relationships | An established friendship or repeat social interaction |
| Activities | People joining a shared experience | Attendance at an activity or event |
| Business | Professionals forming useful connections | A meeting, collaboration, referral or professional relationship |

These outcomes are more useful than using likes, message counts or screen time as universal indicators of success.

## 4. Trust, safety and privacy need their own roadmap

This deserves special attention because Swoovr intends to differentiate itself through accountability and integrity.

The Australian eSafety Commissioner's guidance highlights harassment, unwanted sexual messages, coercive behaviour, privacy risks and abuse following in-person meetings.

### Priority improvements

| Requirement | What I would specify |
|---|---|
| Age assurance | Minimum age 18, with age checks, DOB discrepancy handling, disputed results and re-verification procedures |
| Photo verification | Liveness/photo matching, image-change invalidation, manual review and expiry/revocation |
| Identity verification | Separate from photo verification; verified DOB treated as authoritative after a controlled discrepancy review |
| Ban evasion | Risk-based re-registration detection without indefinite retention of biometric documents |
| Media consent | Recipient-controlled sensitive content, classification before delivery and default blocking of unsolicited explicit images |
| Location protection | Server-side distance controls, reduced location precision and protection against repeated location probing |
| Block/report | Block and report from profiles, chats, messages, media, events and groups |
| Moderation | Case queues, evidence retention, escalation, audit trails, appeals and conflict-free handling of staff reports |
| Meeting safety | Voluntary check-ins, trusted contacts, quick reporting and appropriate emergency guidance |
| User privacy | Clear data export, deletion, retention, consent management and prevention of cross-mode disclosure |

### Three particular issues

**Private explicit albums require a policy decision before implementation.** Apple and Google both restrict sexually explicit user-generated content and services that become primarily associated with objectionable material. Apple permits some incidental mature web-sourced content under limited conditions, but this is not blanket approval for explicit private albums. The existing clean/nudity/explicit classifier is technically useful, but classification alone does not establish app-store compliance.

**The voluntary Australian Online Dating Code is worth adopting as a design benchmark**, whether or not Swoovr subsequently becomes a signatory. It addresses reporting, enforcement, moderation transparency, safety resources and cooperation with law enforcement. eSafety confirms that the code is voluntary and independently overseen rather than enforced by eSafety itself.

**Subscription billing needs a platform-aware model.** Stripe is appropriate for many physical-world bookings, but Apple generally requires in-app purchasing for digital subscriptions and functionality, subject to applicable exceptions and regional rules. Design one entitlement service that can recognise purchases from different payment providers.

I would commission a formal privacy, application-security and app-store-policy review before accepting unrestricted external beta users.

## 5. Proposed new user experiences

These are particularly valuable because they connect multiple existing Swoovr features rather than introducing unrelated features.

### Available for…

A temporary opt-in status for members who want to do something specific: coffee today, dinner tonight, a walking partner this weekend or professional networking after work.

The status expires automatically and can be visible only within selected modes.

### Open invitations

Instead of waiting for a connection, a member creates an invitation such as “Two more people for board games on Friday.”

Other members request to join. The organiser approves participants and the group receives a temporary event chat.

### Shared planning workspace

Two connected people can suggest places, vote on options, choose a time, confirm a meeting and save the plan.

This evolves your existing Plan a Date idea into a reusable tool across Dating, Friends and Business.

### Travel introductions

A person planning to visit Adelaide can opt into local introductions and activities before arrival.

Once their travel period begins, available events, local members and relevant venues become easier to discover.

### Professional introductions

A Business member can request a specific introduction: looking for a mentor, collaborator, supplier or networking partner.

This is a clearer professional interaction than simply copying a dating-style like button.

There is supporting evidence for interest-based social discovery: Bumble's Australian-inclusive 2025 trends research identified hobbies, fan communities and shared interests as important parts of connection for younger singles.

## 6. Recommended roadmap

I would organise the roadmap by dependencies and validated member behaviour, rather than attempting every feature in one release.

### Phase 0 — Foundations and private beta

**Timing:** Immediate priority; existing 60–90-day beta target  
**Status:** Release critical

Deliver registration, profiles, age rules, mode-specific identity and visibility, discovery, contact requests, basic messaging, verification states, blocks, reports, moderation tools, support, notification privacy and analytics.

Include founding-member entitlement handling and actual member onboarding.

**Milestone:** Real testers can safely create a profile, discover a compatible member and communicate.

### Phase 1 — Public MVP and connection quality

**Timing:** After beta validation

Add refined matching explanations, useful discovery filters, conversation starters, saved profiles, Plan a Date, basic places discovery, subscriptions and clearly distinguished local sponsorship.

Validate Free/Plus/Premium economics before finalising limits.

**Milestone:** Members progress from discovery to meaningful, mutually wanted conversations and real-world plans.

### Phase 2 — Communities and activities

**Timing:** First major product expansion

Add open invitations, group activities, RSVPs, event chats, recurring meetups, organiser controls and community moderation.

Pilot genuine Adelaide venue partnerships.

**Milestone:** Members can find activities and form friendships without relying on one-to-one dating matches.

### Phase 3 — Professional and commercial ecosystem

**Timing:** Dependent on network density and demand

Introduce organisation accounts, verified business affiliations, professional introductions, business events and a basic venue-partner portal.

Expand commercial reporting and advertising self-service.

**Milestone:** Professional members and local businesses can independently obtain measurable value from Swoovr.

### Phase 4 — Bookings and advanced experiences

**Timing:** Commercial expansion

Add supported reservations, attraction tickets, payment fulfilment, vouchers, shared purchases, deeper partner integrations and improved travel discovery.

Consider calls and additional AI assistance once the core safety and operational systems are mature.

**Milestone:** Swoovr supports a complete discovery-to-experience transaction where viable.

> These phases are a proposed delivery sequence, not confirmed release commitments. I would preserve the simultaneous iOS, Android and web launch requirement and use feature flags for anything unfinished or still being evaluated.

## 7. Technical architecture implications

The .NET MAUI, Blazor Web App and API direction remains appropriate. I would concentrate on defining product domains and their boundaries before adding more user interfaces.

| Domain | Responsibilities |
|---|---|
| Identity & Assurance | Keycloak integration, verification records, age status, credentials |
| Member Profiles | Base profile, mode-specific data, privacy and visibility |
| Discovery | Eligibility, location, interests, filters, ranking |
| Compatibility | Question bank, weights, answer versions, explanations |
| Connections | Requests, acceptance, disconnection, blocking |
| Messaging | Conversations, messages, media, delivery, consent |
| Activities & Events | Events, invitations, memberships, RSVP, attendance |
| Places & Venues | Venue information, sponsorship, partnerships |
| Commerce | Subscriptions, entitlements, bookings, payments |
| Trust & Safety | Reports, moderation, evidence, appeals, risk controls |
| Notifications | Push, email, SMS, preferences and privacy |
| Analytics | Aggregated funnels, experimentation, commercial reporting |

These are **logical domains, not 12 additional microservices or .NET projects**. They can initially be organised as a modular application, with clear service interfaces and separate data responsibilities.

A few architecture decisions deserve early attention:

- Use explicit authorisation for mode-specific fields and media. Hiding a field in MAUI or Blazor must not be the security boundary.
- Build idempotent event handling for purchases, verification callbacks, notification delivery and reservation confirmations.
- Keep compatibility computation separate from candidate eligibility and discovery ranking.
- Define retention and deletion policies per data category, particularly messages, reports, location information and verification evidence.
- Support moderation access with audited, least-privilege roles rather than ordinary administrative access to all personal information.
- Develop seed-data and simulated-user capabilities for UI demonstrations, clearly isolated from live discovery.

## 8. What I would deliberately avoid

Several features might initially appear attractive but could weaken Swoovr.

| Avoid | Reason |
|---|---|
| Public user star ratings | Encourages retaliation and social scoring |
| Paying to override safety boundaries | Undermines recipient consent |
| Selling preferential compatibility rankings | Makes compatibility less credible |
| Advertising disguised as member profiles | Damages trust |
| Rewarded ads | Inconsistent with the experience already chosen |
| Advertising within private chats | Interrupts a high-value private interaction |
| Artificially populating discovery with bots | Creates misleading perceptions of available members |
| Universal popularity scores | Confuses visibility with compatibility |
| Mandatory public social graphs | Reveals personal, professional and potentially sensitive associations |
| Premature AI-driven matchmaking claims | Hard to justify before real-world outcome data exists |

Official Swoovr bots can still have legitimate support or demonstration uses, provided they are visibly identified and do not impersonate genuine available members.

## 9. What success should look like

There is a distinction between a dating application with many active users and one that actually helps people connect.

I would use the following performance framework during the Adelaide beta.

### Proposed beta scorecard

| Metric | Definition |
|---|---|
| Activation | Percentage completing a useful profile and first discovery session |
| Discovery quality | Percentage finding at least one relevant, contactable member |
| Connection acceptance | Accepted requests divided by requests sent |
| Conversation quality | Percentage of accepted connections with reciprocal communication |
| Real-world outcomes | Voluntarily confirmed plans, meetings or event attendance |
| Safety | Report rates, time to action, repeat incidents and appeal outcomes |
| Retention | 7-day and 30-day retention segmented by mode and member cohort |
| Commercial | Subscription conversion, revenue, support cost and venue-partner results |

Crucially, measure supply and demand within each combination of location, mode, intention and relevant preference. One thousand registrations does not mean one thousand mutually compatible, active people.

With approximately 100 planned Adelaide beta testers, the initial goal should be to identify serious product and safety problems, assess usability, and understand connection behaviour—not to claim statistically reliable outcomes for every demographic or mode.

## 10. The ten decisions I would make next

- [ ] **1. Mode-specific privacy — Before beta.** Which fields, photos, identifiers and presence signals may cross between Dating, Friends, Activities and Business?
- [ ] **2. Connection state machine — Before beta.** Define requests, acceptance, conversations, declined requests, disconnections and blocking per mode.
- [ ] **3. Age and verification policy — Before beta.** Complete DOB conflicts, re-verification, expiry, revocation and account restrictions.
- [ ] **4. Moderation workflow — Before beta.** Define response priorities, evidence rules, appeals, staff escalation and retention.
- [ ] **5. Messaging consent — Before beta.** Set who may message whom, handling of sensitive media, spam controls and conversation termination.
- [ ] **6. Discovery availability — Before beta.** Define how market activation, inactive profiles, small local pools and travel availability interact.
- [ ] **7. Subscription entitlements — Public MVP.** Complete feature limits and digital versus physical-service payment handling.
- [ ] **8. Plan a Date — Public MVP.** Specify proposal, response, rescheduling, venue choice and cancellation lifecycle.
- [ ] **9. Groups and events — Phase 2.** Set basic group creation, RSVP, attendance, invitations and organiser permissions.
- [ ] **10. Business account model — Phase 3.** Distinguish personal professional profiles, organisations, venue partners and advertisers.

## Final assessment

The strongest potential differentiator for Swoovr is not any one feature. It is the combination of four things:

1. **Trust:** Transparent verification, independent credentials, moderation and consent.
2. **Compatibility:** Meaningful intentions, preferences and explainable connections.
3. **Discovery:** People, interests, places, professional contacts and local experiences.
4. **Real-world outcomes:** Turning conversations into dates, friendships, activities and collaborations.

**My highest-priority product addition is the Groups, Events and Open Invitations system.** It gives Friends and Activities modes a purpose independent of Dating, creates repeat engagement, provides venues with genuine commercial opportunities and makes Swoovr useful to people who are not looking for romance.

**My highest-priority architectural improvement is mode-specific privacy and connection lifecycle management.** These are foundational requirements that will become expensive to retrofit once people have established private and professional relationships on the same account.

**My biggest commercial concern is local network density.** Swoovr must be genuinely useful with a small Adelaide community before attempting national expansion. A polished multi-mode app with insufficient relevant members could struggle despite having excellent features.

The product vision is ambitious but coherent. I would maintain the full four-mode vision while building and validating the shared foundations first, then expanding the experiences that demonstrably create real connections.

---

**Research basis:** Prior Swoovr planning discussions through 26 October 2026, Australian eSafety guidance, Apple and Google app-store policies, and published 2025–2026 industry research. This is a product and strategic assessment, not an audit of the actual source repository.

**Research and policy resources for verification:**

- [Australian eSafety Commissioner — Online dating](https://www.esafety.gov.au/)
- [Apple App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/)
- [Google Play Developer Policy Center](https://play.google.com/about/developer-content-policy/)
- [Adjust — Mobile app trends](https://www.adjust.com/resources/reports/)
- [Bumble — The Buzz](https://bumble.com/the-buzz)

> **Policy note:** Private-album compliance discussion reflects the original roadmap review. The follow-up discussion clarified that Grindr and SCRUFF support consensual private nude-image sharing, but competitor availability does not guarantee automatic app-store approval for Swoovr's implementation.
