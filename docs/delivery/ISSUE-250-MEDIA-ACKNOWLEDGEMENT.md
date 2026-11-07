# Media acknowledgement and local helpdesk connectivity

Issue: https://github.com/repasscloud/wanderconnect/issues/250

Observed 8 November 2026: deployed Admin logs show repeated safety acknowledgement POSTs returning 403 while queue GETs return 200. The new blanket privileged-mutation middleware included acknowledgement, and the page replaced all refusals with a checkbox error. DEFAULT correction: recording the required notice is not privileged media access; exempt only this exact POST from elevation. Staff authorization, notice version and confirmation remain enforced. Media reveal and decisions retain their elevation gates. The typed client now preserves refusal codes and the page reports the actual refusal.

The additional `/tickets` report returned 503. The platform's recreated Mailpit network anchor belonged only to `swoovr-platform_default`, while helpdesk nginx belonged to `swoovr-ticketing`. DEFAULT correction: startup reconnects the anchor to an already existing helpdesk network before starting API consumers, preserving the original integration alias and URL.

Validation: Release solution compilation and MFA checks passed during the repository check run. The first full run stopped at PostgreSQL authentication because its legacy `.local/docker.env` differs from the managed platform credentials; rerun uses the managed credentials without printing them. The earlier isolated media browser runner timed out during fictional staff sign-in and cleaned up its owned resources, so authenticated browser acceptance remains unverified. No shared database reset, merge or deployment was performed.

Final verification: `./scripts/check.sh` passed with managed database credentials (exit 0), including 25 MFA policy checks, 765 PostgreSQL persistence cases and all three HTTP smoke suites. Launcher Node tests and Bash syntax validation passed.
