# Venue experiences, vouchers and commerce

## 1. Role in the product

CONFIRMED: partner places willing to accommodate voucher-based dates/meetings. Optional experiences support dating and platonic meetings; users remain free to meet independently. The exact comparator described as “whisper” has not been identified. Breeze is a research reference for partner-venue organised dates, not a confirmed identity or specification to copy.

## 2. Recommended pilot model

DEFAULT: a venue-specific experience, such as coffee for two, clearly priced and redeemable under defined terms. No generic in-app currency. No compulsory restaurant discounts, no assumption of venue reimbursement economics. Whether venues fund discounts, receive advance payments or pay commission is OPEN. Before live sales, determine merchant of record, tax, liability, refund ownership and settlement agreement.

Offer fields: venue, location, accessibility facts, inclusions/exclusions, participant count, price/currency, available times, capacity, booking cut-off, redemption window, cancellation policy, dietary options, age/alcohol restrictions where applicable, terms version and settlement arrangement. Alcohol must not be required for participation; provide non-alcohol options.

## 3. End-to-end flow

1. Connection proposes an experience/time. Both confirm attendance and material terms.
2. Backend reserves capacity with an expiry while checkout completes. A proposal alone is not a confirmed booking.
3. Show who pays: DEFAULT one organiser pays for the complete experience; split payments deferred because partial completion complicates refunds/capacity. Both must consent to the plan regardless of payer.
4. Payment confirmation is verified server-side; reconcile asynchronous payment and capacity state.
5. Confirm booking and issue its experience-specific voucher only after the required successful payment and venue confirmation. Pending venue approval remains pending, with no valid voucher.
6. Users see venue, booking, inclusions, redemption instructions and cancellation support.
7. Authorised venue staff scan a short-lived opaque QR or enter a code; backend validates booking, venue scope, validity and unused status, then atomically redeems.
8. Give both parties a receipt/status and offer independent private feedback after the meeting.

Voucher payload contains no legal identity, private dating intention or reusable payment secret. A copied QR must not allow double redemption. DEFAULT one booking voucher for the experience, shared to attendee wallets with the same server redemption state. Staff see only information needed to honour the booking.

## 4. State models

Keep payment, booking and voucher state separate.

- Payment: pending, succeeded, failed, refund pending, partially refunded, refunded, disputed.
- Booking: proposed, awaiting confirmations, held, awaiting payment, awaiting venue, confirmed, completed, cancelled, expired.
- Voucher: pending, issued, redeemed, expired, void.

Only valid combinations are allowed. Late successful payment after hold expiry requires reallocation or refund workflow, never overselling. Duplicate webhooks are idempotent. Refund/chargeback can void an unused voucher; redeemed disputes need staff handling. Venue cancellation offers rebooking or refund under disclosed terms. No-show reports are not automatic proof either party is at fault. Staff cannot redeem another venue's voucher.

## 5. Monetisation

Separate three product types: digital premium subscription; real-world venue experience; promotional venue discount entitlement. Do not sell a digital boost and call it a physical voucher to avoid billing requirements.

DEFAULT free baseline: meaningful profile, ordinary local discovery, core intention exclusions, sparse filter, block/report, basic eligible messaging and private feedback. Paid convenience candidates: saved filter sets, extended discovery, optional incognito and travel planning tools; final free/paid allocation OPEN. Paid status never overrides another member's visibility/contact restrictions. Safety controls stay free. No final price approved.

Support Stripe web purchases and Apple/Google digital billing where required. App-store treatment depends on region and product; Netflix's reader exception is not a universal dating-app exception. Physical venue services have different treatment from digital unlocks. Regional external-link rules must be configuration-driven and reviewed. Do not assume “web-only upgrades” is valid globally.

## 6. Entitlements and subscriptions

Central account entitlement ledger receives verified Apple/Google/Stripe events; source provider remains billing authority. Map provider product IDs to internal offers. Handle restore, renewal, expiration, grace, refunds and revocation; do not trust client success screens. Prevent accidental duplicate subscriptions and link existing provider purchases carefully to an authenticated account. Show billing source and correct management destination. A central account can use entitled access on other clients subject to platform rules. Never assume Stripe can cancel an Apple subscription.

## 7. Venue portal

Venue onboarding and approval; staff roles; offer availability; booking calendar; scanning/manual redemption; settlement statements; cancellation/support; staff audit trail. No access to users' dating preferences, identity documents, private feedback or chat. Administrator can suspend venue offers and resolve booking exceptions. Prototype must include one successful redemption and one denied duplicate redemption.
