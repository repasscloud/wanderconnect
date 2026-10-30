# Accessibility release gate

D271 DEFAULT implements issue48 under the owner's Phase0 queue. Accessibility is a release requirement. Passing automation alone does not approve an external UAT release or establish WCAG compliance.

Before requesting UAT deployment approval, attach the actual commit, date, tester, device/OS/browser and results for the following finite checks. Any failure that prevents registration, profile editing, discovery, consent, messaging, blocking/reporting, support or account rights blocks release until corrected and retested. Do not infer success from a build or a DOM attribute.

| Gate | Required evidence | Current evidence |
|---|---|---|
| Repository | `./scripts/check.sh`, no hidden skipped persistence | Actual results in VALIDATION.md |
| Web automated | `tests/accessibility/pages.browser.cjs` at390/320px; `interactions.browser.cjs` keyboard skip links, rendered personal theme modes,200% text, reduced motion, forced colors, staff pages | Actual results in VALIDATION.md |
| Web human review | Keyboard-only core journey and visible focus; browser zoom200%/400%; screen reader names, error/status announcements, heading/navigation order | OPEN: automation does not establish these human results |
| Android | TalkBack core journey, large system font/display settings, touch-target access and usable alternatives to complex gestures | OPEN: compile/API evidence is separate |
| iOS | VoiceOver core journey and large Dynamic Type; physical or simulator build identified | OPEN: simulator compilation does not establish OS UI behavior |
| Essential alternatives | Copyable text/link wherever a QR is offered; text equivalent/captions for any introduced audio/video; buttons for gesture-only operations | Inspect the actual shipped surfaces; absence of audio/video is N/A, never a caption pass |

Disability/access information remains optional and explicitly controlled by members. Never infer needs or matching eligibility from it. This slice introduces no new profile field, matching rule, telemetry, forced theme or tracking of assistive-technology use.

The public/member and staff skip links must move actual keyboard focus into a programmatically focusable main region on the same current path/query, including pages beneath a root base URL. Staff pages retain Standard; member themes remain personal choices from the read-only design source. The rendered-theme matrix exercises existing CSS; it is not a design-token edit or an exhaustive contrast audit of every component.200% CSS text resizing is labelled separately from actual browser zoom. Forced-color/media emulation is labelled separately from an OS/device check.

Run both browser suites through the owned runner in TESTING.md after a completed Debug build. Never rebuild while browsers run. Keep screenshots fictional and avoid real staff/member records. Record unresolved findings and manual gaps in the UAT handoff; the deployment approval must explicitly review them.

Implementation: both layouts have a focusable main and same-page fallback link. Each host ships the same small delegated `skip-main.js` handler in its own wwwroot; it explicitly focuses main on unmodified activation and avoids navigation/history writes. Actual browser tests verify both hosts. No cross-project dependency is added. An attempted linked static asset returned an empty body in this local runtime despite its manifest entry; ordinary host-owned static files avoid that unverified packaging path.
