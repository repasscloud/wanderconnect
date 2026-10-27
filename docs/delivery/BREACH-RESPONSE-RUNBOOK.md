# Data breach and security incident response runbook

Status: DEFAULT (builder, 27 October 2026; #89, internal policy §12, police-contact procedure §12). Needs legal and operational review before the external beta (#57). Not legal advice.

This is what Swoovr staff do when something may have gone wrong with personal information: unauthorised access, disclosure, loss or alteration. It covers the Australian Notifiable Data Breaches (NDB) scheme under the Privacy Act 1988. Act at once; don't wait for certainty.

## Who

- **Incident lead**: the security and legal officer (`legal` role). If they're unavailable or implicated, the root admin appoints an uninvolved lead.
- **Escalation contacts**: the addresses in `SWOOVR_ESCALATION_EMAIL` and `SWOOVR_ESCALATION_BACKUP_EMAIL` (D159). Automatic alerts go there with ids only.
- **Staff implicated** in an incident are excluded from handling it (procedure §5): record a conflict of interest under **Elevated access**, and revoke their access if needed (**Elevated access → Revoke a staff member**).
- **Legal counsel** for any notifiable decision, law-enforcement involvement or contested question.

## Severity levels

| Level | Meaning | Examples | Start |
| --- | --- | --- | --- |
| Sev1 critical | Confirmed unauthorised access to, or loss or alteration of, identity, evidence or message data; a broken hash chain | Custody or access-log chain broken; database dump found; evidence package sent to the wrong recipient | Immediately, any hour |
| Sev2 high | Suspected unauthorised access to restricted data, or a compromised staff account | Staff credentials phished; unexplained identity openings | Within 1 hour |
| Sev3 medium | An unusual access pattern or bulk export that needs review | More than 3 identity openings by one person in an hour; a bulk evidence export | Same business day |
| Sev4 low | A policy deviation with no data exposed | A purpose recorded too vaguely | Within 5 business days |

Automatic incidents (admin app **Security**): `unusual-identity-access` and `bulk-export` open as Sev3; `custody-chain-broken` and `access-log-chain-broken` as Sev1. Anyone in the `legal` role or the root admin can open one manually.

## Steps

1. **Record** — open an incident in the admin app (**Security → Open an incident**) with the severity and what happened, using ids, never member content. The detection time starts the 30-day assessment clock.
2. **Contain** — stop it getting worse: end the person's elevated access or revoke the staff member; rotate affected keys (`keys rotate <set>` after adding the new version, see below); disable a leaked link; ask a wrong recipient to delete and confirm. Mark the incident **Contained**.
3. **Preserve** — place a legal hold over affected accounts if evidence may be needed (procedure §7); keep logs (the sensitive-access log is append-only; the custody chains are hash-linked). Don't destroy or rewrite anything.
4. **Assess** — is it an *eligible data breach*? All three must be true: (a) unauthorised access to, or disclosure or loss of, personal information; (b) likely to result in serious harm to any of the people it's about; (c) Swoovr hasn't been able to prevent that likely harm through remedial action. Consider the kind of information (identity records, messages, contact history and evidence are sensitive), who has it, and the harm (physical safety, stalking, discrimination, financial, reputational). **Start at once; finish within 30 days.** Record the decision and the reasons (**Record assessment**).
5. **Notify** — if eligible: prepare the statement to the OAIC (Swoovr's identity and contact details, a description of the breach, the kinds of information, recommended steps for people) and notify the affected people (directly, or by publishing the statement if direct notice isn't practicable) as soon as practicable. If law enforcement is involved, follow the police-contact procedure; never delay safety-critical notices without legal advice.
6. **Review** — find the root cause, fix it, record the remediation and close the incident (closing needs the assessment). Major incidents get an immediate targeted review (procedure §13).

## Tools in the prototype

- Admin app **Security** (incidents with severity, the 30-day assessment due date, contained/closed), **Elevated access** (grants, conflicts, bulk approvals, the access log with its chain status, staff revocation), **Legal holds** and **Law enforcement**.
- `keys status` — which key version seals how many values in each set (contact history, evidence, police references, identity store).
- `keys rotate contact-history|evidence|police|identity|all [--to n]` — after adding a new key version to configuration (`ContactHistory:Keys:{n}`, `Evidence:Keys:{n}`, `IdentityStore:Keys:{n}`; Key Vault when hosted), re-seals every value under it. Older versions stay readable until re-sealed, so the hosts keep working while it runs; remove an old key only after `keys status` shows nothing left under it. Evidence hashes don't change (they are of the plain content).

## Not built yet (hosting)

Central log monitoring and alert routing beyond email, database access auditing at the server, tested encrypted backups, penetration testing, vulnerability disclosure, supplier risk assessments (Didit, hosting, Twilio, moderation providers) and Key Vault (#89, #57).
