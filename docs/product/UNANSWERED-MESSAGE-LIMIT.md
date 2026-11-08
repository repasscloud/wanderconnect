# Optional profile-wide unanswered-message limit

Standalone feature tracked in [#300](https://github.com/repasscloud/wanderconnect/issues/300). Requirements preserved from the earlier mixed planning document; implementation is not claimed.

## Objective

Give members a voluntary messaging boundary to reduce repeated unanswered messages without forcing a member to block or disconnect someone, while preserving normal adult communication.

## Key decision: OFF by default

**Default for all new and existing accounts: Off (unlimited).** A member may enable the profile-wide setting and select **3, 5 or 10** consecutive unanswered messages. Do **not** set the default to 5; earlier draft language suggesting that was superseded by the explicit decision to default **Off**.

## Requirements

- One **global profile/privacy setting per recipient**; it applies to that member's incoming messages across all one-to-one conversations. **No per-conversation overrides**, no relationship-based algorithm and no time-based reset.
- If Member B enables a limit of 5, Member A may send five consecutive messages to B without a response. The sixth is rejected until B replies at least once, even if the five messages were sent over weeks or years.
- The converse direction is assessed independently using Member A's own chosen receiving limit.
- Any successful recipient reply—even a one-word reply—resets the other member's consecutive unanswered-message count for that conversation.
- Opening, reading, marking as unread, editing or deleting messages does not reset the counter. Message attachment types count in a consistent way; system messages, typing/read receipts do not.
- Changing a recipient's profile setting takes effect immediately, including on existing conversations. Disabling the limit permits messaging subject to all other access and safety rules; re-enabling evaluates the existing unanswered sequence.
- The Swoovr API enforces it atomically across web, iOS and Android; clients show a neutral message such as: "You've reached the unanswered message limit for this conversation. You can send more when they reply."
- Don't disclose the exact recipient preference unless there is an established UX reason; don't pressure the recipient to reply.
- Preserve reporting, blocking, disconnecting and existing membership-tier restrictions; this is a safety/comfort feature available to **Free and paid members**.

## Tests / acceptance

- [ ] Default is Off for new and existing members, with no messaging restriction imposed by this feature.
- [ ] Off / 3 / 5 / 10 settings persist across devices and sessions.
- [ ] Message at threshold succeeds; next unanswered message is rejected.
- [ ] Reply resets allowance; read receipt and elapsed time do not.
- [ ] Text, media and supported attachments count; system messages do not.
- [ ] Independent counts in each direction and each conversation.
- [ ] Edit/delete/clock-wait workarounds fail.
- [ ] Changing 10 to 3 immediately restricts someone with 5 unanswered messages; turning Off permits messages again.
- [ ] Re-enabling does not inadvertently wipe unanswered history.
- [ ] Concurrent sends cannot bypass limit; rejected sends do not become persisted messages.
- [ ] Web/iOS/Android provide consistent sender feedback.

## Clarifications before implementation

- Determine how the current data model represents conversations, participants and message deletion/editing, then select a method that reliably counts the consecutive sequence without expensive per-send queries.
- Define whether group conversations, reaction-only events and support/staff/system messages are out of scope. The intended initial target is member-to-member one-to-one chat.
- Avoid modifying existing messaging tier limits without review.

