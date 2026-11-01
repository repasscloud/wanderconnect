# Member responsive layout

CONFIRMED: retain Swoovr's monochrome default, Helvetica typography, badges and components; personal schemes remain private choices. Member web only. No merge, deployment or Actions changes. Design references are read-only.

Original constraint: almost every SSR member route wraps its interactive panel in `.column` (420px), including discovery, connections and messages. Outer `main` already allows 1100px; increasing it alone cannot fix the problem. Fixed 170px tiles are intentional in the confirmed UI reference.

DEFAULT implementation choices:
1. Keep the 1100px shell and 420px form columns. Add explicit browsing, profile, conversation and reading widths to routes, including their guest/error/loading states. Do not change authorization, providers or data projections.
2. Discovery uses its full shell with existing equal tiles; cap mode controls and notices at readable widths. Connections use two columns when each can comfortably fit. Chat lists use roomy rows; individual conversations use a bounded reading/composition width. Profile sections use two columns without changing DOM reading order or privacy boundaries.
3. Account summary and settings use two columns on desktop, while embedded sensitive forms stay bounded. Safety/legal articles get a readable wider measure. All remaining editor, matching, verification, deletion and safety report forms stay narrow. Home uses a balanced introduction and existing information card.
4. Harden wrapping, dialog sizing, safe-area navigation, request actions and text reflow. Use existing semantic tokens only.

Execution and evidence:
- Capture baseline fictional-adult guest and authenticated pages at375/768/1024/1440 before CSS changes; prove the desktop constraint through measured geometry.
- Add browser coverage of those widths, Standard light/dark, all personal palettes, keyboard navigation,200% text and320px reflow; inspect representative PNGs.
- Build Debug before browser runs; use existing owned database/API runner and restore positively identified MAIN hosts. Temporarily preserve/stop managed Docker installation to use legacy sandbox; restore original managed containers after acceptance. No shared configuration edits.
- Run affected existing browser/accessibility/appearance suites sequentially and check.sh before commit. Record failures and gaps honestly in delivery docs.
- Review diff, stage only task files, commit, push an explicit branch refspec and open PR against main with before/after evidence. Stop there.

OPEN: no owner decision needed for these reversible layout defaults. Hosted/device and full assistive-technology acceptance are outside this local web change.
