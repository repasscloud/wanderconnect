# Data breach and security incident response runbook

Status: DEFAULT (builder, 27 October 2026; revised 2 November 2026; #89, internal policy §12, police-contact procedure §12). Release candidate for legal and operational review before the external beta (#57). Not legal advice.

This is what Swoovr staff do when something may have gone wrong with personal information: unauthorised access, disclosure, loss or alteration. It implements the Notifiable Data Breaches (NDB) scheme in Part IIIC of the Privacy Act 1988. Act at once; don't wait for certainty.

## Who

- **Incident lead**: the security and legal officer (`legal` role). If they're unavailable or implicated, the root admin appoints an uninvolved lead.
- **Escalation contacts**: the addresses in `SWOOVR_ESCALATION_EMAIL` and `SWOOVR_ESCALATION_BACKUP_EMAIL` (D159). Automatic alerts go there with ids only.
- **Staff implicated** in an incident are excluded from handling it (procedure §5): record a conflict of interest under **Elevated access**, and revoke their access if needed (**Elevated access → Revoke a staff member**).
- **Legal counsel** for any notifiable decision, law-enforcement involvement or contested question.
- **Communications**: only the incident lead approves anything said to members, providers, media or regulators.

## Severity levels

| Level | Meaning | Examples | Start |
| --- | --- | --- | --- |
| Sev1 critical | Confirmed unauthorised access to, or loss or alteration of, identity, evidence or message data; a broken hash chain | Custody or access-log chain broken; database dump found; evidence package sent to the wrong recipient | Immediately, any hour |
| Sev2 high | Suspected unauthorised access to restricted data, or a compromised staff account | Staff credentials phished; unexplained identity openings | Within 1 hour |
| Sev3 medium | An unusual access pattern or bulk export that needs review | More than 3 identity openings by one person in an hour; a bulk evidence export | Same business day |
| Sev4 low | A policy deviation with no data exposed | A purpose recorded too vaguely | Within 5 business days |

Automatic incidents (admin app **Security**): `unusual-identity-access` and `bulk-export` open as Sev3; `custody-chain-broken` and `access-log-chain-broken` as Sev1. Anyone in the `legal` role or the root admin can open one manually. Raise severity as soon as facts justify it; never lower it without recording why.

## Steps

1. **Record**: open an incident in the admin app (**Security → Open an incident**) with the severity and what happened, using ids, never member content. The time Swoovr became aware of a suspected breach starts the 30-day assessment clock.
2. **Contain**: stop it getting worse. End the person's elevated access or revoke the staff member; rotate affected keys (`keys rotate <set>` after adding the new version, see below); disable a leaked link; ask a wrong recipient to delete the material and confirm in writing. Mark the incident **Contained**.
3. **Preserve**: place a legal hold over affected accounts if evidence may be needed (procedure §7); keep logs (the sensitive-access log is append-only; the custody chains are hash-linked). Don't destroy or rewrite anything.
4. **Assess**: decide whether it is an *eligible data breach*. All three must be true: (a) unauthorised access to, or disclosure or loss of, personal information; (b) a reasonable person would conclude it is likely to result in serious harm to any of the people it's about; (c) Swoovr hasn't been able to prevent that likely risk of serious harm through remedial action. Consider the kind of information (identity records, messages, sexual orientation, contact history and evidence are highly sensitive on a dating service), who has it, whether it is encrypted and the keys are safe, and the harm (physical safety, stalking, outing, extortion, discrimination, financial, reputational). **Start at once; finish within 30 days; notify as soon as practicable once there are reasonable grounds to believe it is eligible.** Record the decision and the reasons (**Record assessment**).
5. **Notify**: if eligible, follow the notification checklist below. Never delay a safety-critical notice without legal advice; if law enforcement asks for a delay, record the request and get counsel's advice, because the NDB scheme only allows limited exceptions.
6. **Review**: find the root cause, fix it, record the remediation and close the incident (closing needs the assessment). Major incidents get an immediate targeted review (procedure §13). Closed incident records follow the 24-month security-incident retention (D262).

## Notification checklist

| Who | When | How and what |
| --- | --- | --- |
| OAIC | As soon as practicable after reasonable grounds to believe the breach is eligible | NDB statement through the OAIC online form, containing: Swoovr's identity and contact details (RePass Cloud Pty Ltd, ABN 74 642 243 801, <privacy@swoovr.com>); a description of the breach; the kinds of information involved; and the steps people should take in response. |
| Affected people | As soon as practicable after the OAIC statement | Choose one: notify everyone whose information was involved; notify only those at risk of serious harm; or, if direct notice isn't practicable, publish the statement on swoovr.com and take reasonable steps to publicise it. Use the account notice centre and email; never include more personal information than needed. Include support and safety resources. |
| Providers jointly holding the information (Didit, Twilio, MailerSend, Cloudflare, Microsoft, Apple, Google) | At once, if their systems or data are involved | Coordinate one assessment and agree who notifies; only one entity needs to notify for a breach held jointly. Record the agreement. |
| Police | Where criminal conduct is suspected or evidence was taken | Follow the police-contact procedure; this is a referral, not a disclosure of member data without a lawful basis. |
| Australian Signals Directorate (ReportCyber) | Recommended for cyber incidents | Voluntary report; information given to ASD has limited-use protections. |
| Ransomware payment report | Within 72 hours of making or becoming aware of a payment, if Swoovr is above the Cyber Security Act 2024 turnover threshold | Counsel confirms the threshold position; paying a ransom needs director approval and legal advice. |
| eSafety Commissioner | Where intimate images or other harmful material about members has been exposed | Help affected members use eSafety's reporting and removal schemes; consider Swoovr's own reporting obligations under the Online Safety Act. |
| EU or UK supervisory authority, and affected EU or UK members | Within 72 hours of becoming aware, where the GDPR applies and the breach is likely to risk people's rights; members without undue delay where the risk is high | Through the Article 27 representative and the lead authority; record every breach in the GDPR breach log even if not notified. |
| US state regulators and residents | Within the deadline of each applicable state law | Counsel identifies the states from affected members' home regions. |
| Insurer and directors | As soon as practicable | Under the policy's notice terms. |

## Tools in the prototype

- Admin app **Security** (incidents with severity, the 30-day assessment due date, contained and closed states), **Elevated access** (grants, conflicts, bulk approvals, the access log with its chain status, staff revocation), **Legal holds** and **Law enforcement**.
- `keys status`: which key version seals how many values in each set (contact history, evidence, police references, identity store).
- `keys rotate contact-history|evidence|police|identity|all [--to n]`: after adding a new key version to configuration (`ContactHistory:Keys:{n}`, `Evidence:Keys:{n}`, `IdentityStore:Keys:{n}`; Key Vault when hosted), re-seals every value under it. Older versions stay readable until re-sealed, so the hosts keep working while it runs; remove an old key only after `keys status` shows nothing left under it. Evidence hashes don't change (they are of the plain content).

## Readiness before production

Not built or not yet tested in the hosted environment (#89, #57):

- Central log monitoring and alert routing beyond email, and database access auditing at the server.
- Tested encrypted backups and restore with deletion and hold reconciliation (D261).
- Independent penetration testing and a published vulnerability disclosure route.
- Supplier risk assessments and breach-notice clauses for Didit, Microsoft, Cloudflare, Twilio, MailerSend and the moderation providers.
- Key Vault in production.
- A tabletop rehearsal of this runbook at least once before the external beta and annually afterwards, with the result recorded in the compliance register.
