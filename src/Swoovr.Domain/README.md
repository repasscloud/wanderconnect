# Domain

Pure business entities and rules belong here. No HTTP, database, UI or provider SDK dependencies.

`Access/AccountAccessPolicy.cs` implements the coarse account-area gate for member, venue partner, admin and support accounts. Inputs are trusted server snapshots, never request DTOs or guessed provider claims. It is not yet wired to API authorization. See [the phase 1 slice](../../docs/delivery/PHASE-1-ACCESS-POLICY.md) for limitations and required integration work.

`Discovery/` adds explicit ANY/ALL required intentions, ANY exclusions and shared list/profile/media/saved/new-contact eligibility using trusted server snapshots. No HTTP authorization or live verification/location source exists. See [discovery slice](../../docs/delivery/PHASE-2-DISCOVERY-POLICY.md). Existing accepted conversations must use their own membership policy rather than this new-contact gate.
