# Phase 2 — portable appearance synchronization state

Implement the client-side account lifecycle specified by [appearance storage rules](../design/USER-APPEARANCE-THEMES.md), without inventing an account API/provider. This is a pure portable state machine for future Web/Mobile adapters, not a connected account service. Guest web settings remain guest-only.

DEFAULT D21: each account activation gets a new opaque in-memory generation. Until its account read completes, display defaults and disallow account edits rather than showing the previous account's preference. Existing account preference wins at sign-in; only an account with no preference initializes from the deliberate generic guest preference. Sign-out clears every account preference, pending save/conflict and in-flight ticket; the generic guest preference remains by policy. Late load/save/conflict responses from a prior activation are ignored, even when the same account later signs in again.

Use explicit stable scheme/mode DTO strings; generate supported scheme IDs from themes.json. Invalid local selections throw before mutation; malformed server snapshots cannot become state. Server revisions are positive monotonic integers; expected revision 0 creates a missing preference. The server must authenticate the account, authorize writes and atomically enforce revisions/idempotency; no server implementation is claimed here. Client timestamps never choose a winner.

One save attempt may be in flight per generation. A save captures an immutable desired pair, expected revision and random change ID. Retries reuse that request until an acknowledged success or conflict. A newer explicit choice remains visible while an older save finishes; its next save uses the acknowledged revision. A duplicate/late response cannot roll back the visible choice. Same-choice edits do not create spurious changes.

On conflict, retain the local choice and expose the server choice/revision. Pause automatic saving until an explicit resolution: use the server choice, or keep the local choice rebased on that authoritative revision. Newer explicit local edits during conflict remain local until resolution. Offline failure retains current and the exact retry attempt; no clock-based ordering. No account identifiers, tokens or entitlements appear in DTOs or persistence.

Plan: add named executable checks for account-switch/reauth isolation, delayed load/save, offline idempotent retry, conflict resolution, malformed revisions and independent dimensions; observe missing-type build failure; implement only Contracts/Client plus generated client catalogue; run full checks and independent review; record evidence and dependent PR. No UI/API integration, persistence, real accounts or mobile/device acceptance is implied.

Server absence must be established by a successful authorized read that explicitly represents no preference, never a network/authentication failure or generic unavailable response. Revisions reserve long.MaxValue so incrementing a request cannot overflow. State mutation belongs to the client UI thread; this is not a multi-threaded server store.

## Unsupported account-value correction —17 October2026

GitHub review on dependent PR#13 identified that unsupported stored values left CompleteLoad permanently loading. A failing regression reproduced it. Valid-revision loads and conflict snapshots now normalize an unknown/removed/malformed scheme/mode pair to Electric violet + System, retaining the exact server revision and completing the load/conflict path. No automatic overwrite is queued; a later explicit edit saves against the retained revision. Invalid revisions still fail closed and permit a valid correlated retry; save acknowledgements still require exact valid posted content.

Sixty appearance-state cases cover unknown scheme/mode/null fields, fallback editing/revision preservation and conflict resolution, plus existing generation/signout/offline guarantees. Corrected at originating PR#8 and explicitly carried through dependent proposals. Pure client state only; no account transport/persistence or UI sync claim.
