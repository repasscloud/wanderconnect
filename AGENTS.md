# Repository instructions

Read README, the relevant product/design specifications and delivery documents before editing. Preserve CONFIRMED requirements; label DEFAULT and OPEN decisions. Keep `DatingApp` as the code namespace and WanderConnect as a replaceable public working name.

- Use `codex/` branches and PRs targeting `main`; do not merge or deploy without an explicit request.
- Keep GitHub Actions files fully commented until the owner re-enables minutes. Run `./scripts/check.sh` before committing; record actual evidence and gaps.
- Respect the project dependency rules in docs/architecture/README.md. Do not add extra projects by habit.
- IdentityCloak is the authentication backend. Do not rename it to Keycloak. Do not invent endpoints or claim configured integration. Venue partner/admin/support must never use social sign-in; MFA is optional.
- Theme values originate in docs/design/themes.json. Run `python3 scripts/check_design.py --write` after token edits, then run checks. Never turn six personal schemes into one global branding choice.
- Ask questions using docs/delivery/DECISIONS.md and track them in QUESTIONS.md. Continue independent work when a provider or owner decision blocks a slice.
- Do not commit credentials, real identity material, private messages or production exports. Prototype identities are fictional adults. Never label simulated verification/payment as live.
- No subagent requirement. Preserve unrelated changes and stage only task-owned files.
