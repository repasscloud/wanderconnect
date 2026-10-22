# Repository instructions

Read README, the relevant product/design specifications and delivery documents before editing. Preserve CONFIRMED requirements; label DEFAULT and OPEN decisions. Use the confirmed public name `Swoovr`, `Swoovr` for .NET namespaces/projects and `swoovr` for lowercase technical identifiers (owner, 22 October 2026). See docs/architecture/NAMING.md.

- Use a descriptive branch name (e.g. `fix/login-validation` or `feature/user-settings`) and open PRs targeting 
  `main`; do not merge or deploy unless explicitly requested.
- Keep GitHub Actions files fully commented until the owner re-enables minutes. Run `./scripts/check.sh` before committing; record actual evidence and gaps.
- Respect the project dependency rules in docs/architecture/README.md. Do not add extra projects by habit.
- Keycloak is the authentication backend (owner correction, 16 October 2026). Do not invent endpoints or claim configured integration. Venue partner/admin/support must never use social sign-in; MFA is optional. An account may have direct and linked social credentials; do not infer an exclusive account type from email. Privileged access requires verified email and a fresh direct email/password or approved email-link session; linked Apple/Google identities are allowed. Validate server-side session evidence before enabling access.
- Theme values originate in docs/design/themes.json. Run `python3 scripts/check_design.py --write` after token edits, then run checks. Never turn six personal schemes into one global branding choice.
- Ask questions using docs/delivery/DECISIONS.md and track them in QUESTIONS.md. Continue independent work when a provider or owner decision blocks a slice.
- Do not commit credentials, real identity material, private messages or production exports. Prototype identities are fictional adults. Never label simulated verification/payment as live.
- No subagent requirement. Preserve unrelated changes and stage only task-owned files.
