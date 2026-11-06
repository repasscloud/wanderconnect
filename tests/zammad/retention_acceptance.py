#!/usr/bin/env python3
"""D337 (#224) acceptance against the running local platform and real local Zammad (fictional data only).

Creates fictional Zammad tickets through Zammad's own Rails runner, backdates their close time, and runs the API's real
`retention purge` / `retention helpdesk --reconcile` commands inside the API container:
  1. a General Support ticket closed 25 months ago is deleted through a data privacy task; its email-only customer goes too;
  2. a Legal & Law Enforcement ticket closed 25 months ago is kept (7 years);
  3. a General Support ticket closed 25 months ago but linked to an open case is kept ("open-case");
  4. a reopened ticket is tracked as open with no end date;
  5. a finally deleted member's customer is retired (inactive, opaque retired- login, name cleared) and, with no tickets, deleted;
  6. after a simulated restore, `--reconcile` retires a reverted customer again and deletes an already-disposed ticket again.
Zammad processes data privacy tasks on its scheduler; the script runs that job directly so it needn't wait ten minutes.
Usage: python3 tests/zammad/retention_acceptance.py
"""
import json, os, secrets, subprocess, uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ENV = dict(line.split('=', 1) for line in (ROOT / '.local/platform/docker.env').read_text().splitlines() if '=' in line and not line.startswith('#'))
DB_PASSWORD = ENV['SWOOVR_DB_PASSWORD'].strip('\'"')
RUN = secrets.token_hex(4)
checks = 0


def check(ok, label):
    global checks
    if not ok: raise SystemExit('FAIL: ' + label)
    checks += 1
    print('PASS: ' + label)


def rails(code):
    out = subprocess.run(['docker', 'exec', '-i', 'swoovr-zammad-zammad-railsserver-1', 'bundle', 'exec', 'rails', 'runner', '-'],
                         input='UserInfo.current_user_id = 1\n' + code, capture_output=True, text=True, check=True).stdout
    return json.loads([line for line in out.splitlines() if line.startswith('SWOOVR=')][-1][7:])


def sql(statement):
    # The password reaches docker through the environment, never argv, so it can't appear in a traceback or process list.
    result = subprocess.run(['docker', 'exec', '-e', 'PGPASSWORD', 'swoovr-platform-postgres-1', 'psql', '-h', '127.0.0.1', '-p', '5433',
                             '-U', 'swoovr', '-d', 'swoovr', '-v', 'ON_ERROR_STOP=1', '-Atc', statement], capture_output=True, text=True,
                            env={**os.environ, 'PGPASSWORD': DB_PASSWORD})
    if result.returncode: raise SystemExit('FAIL: SQL refused: ' + result.stderr.strip()[:300])
    return result.stdout.strip()


def cli(*args):
    result = subprocess.run(['docker', 'exec', 'swoovr-platform-api-1', 'dotnet', 'Swoovr.Api.dll', *args], capture_output=True, text=True)
    return result.returncode, result.stdout + result.stderr


def purge():
    code, out = cli('retention', 'purge')
    helpdesk = [line for line in out.splitlines() if line.startswith('Helpdesk:')]
    check(helpdesk and 'not configured' not in helpdesk[0], 'retention purge ran the helpdesk step: ' + (helpdesk[0] if helpdesk else out[-300:]))
    return out


def process_privacy_tasks():
    rails('DataPrivacyTask.where(state: "in process").find_each(&:perform)\nputs "SWOOVR=" + {done: true}.to_json')


TICKET = '''
c = User.find_by(login: %(login)s) || User.create!(login: %(login)s, email: %(login)s, firstname: "Fictional", lastname: "Retention", roles: [Role.find_by!(name: "Customer")], active: true)
t = Ticket.create!(title: %(title)s, group: Group.find_by!(name: %(group)s), customer: c, state: Ticket::State.find_by!(name: "new"), priority: Ticket::Priority.find_by!(name: "2 normal"))
Ticket::Article.create!(ticket_id: t.id, type: Ticket::Article::Type.find_by!(name: "note"), sender: Ticket::Article::Sender.find_by!(name: "Customer"), body: "Fictional retention check.", internal: false, content_type: "text/plain")
t.update!(state: Ticket::State.find_by!(name: "closed"))
t.update_columns(last_close_at: 25.months.ago, close_at: 25.months.ago)
%(after)s
puts "SWOOVR=" + {id: t.id, customer: c.id}.to_json
'''


def ticket(title, group, login, after=''):
    return rails(TICKET % {'login': json.dumps(login), 'title': json.dumps(title), 'group': json.dumps(group), 'after': after})


def exists(ticket_id):
    return rails(f'puts "SWOOVR=" + Ticket.exists?({int(ticket_id)}).to_json')


def user(user_id):
    return rails(f'u = User.find_by(id: {int(user_id)}); puts "SWOOVR=" + (u && {{login: u.login, active: u.active, email: u.email.to_s, firstname: u.firstname, lastname: u.lastname.to_s}}).to_json')


def retiree(with_ticket):
    account = uuid.uuid4()
    login = f'swoovr-{account}'
    created = rails(f'u = User.create!(login: "{login}", email: "fictional.retiree.{RUN}.{account.hex[:6]}@example.test", firstname: "Swoovr", lastname: "Customer", roles: [Role.find_by!(name: "Customer")], active: true)\n'
                    + (f't = Ticket.create!(title: "Retiree {RUN}", group: Group.find_by!(name: "General Support"), customer: u, state: Ticket::State.find_by!(name: "open"), priority: Ticket::Priority.find_by!(name: "2 normal"))\n'
                       'Ticket::Article.create!(ticket_id: t.id, type: Ticket::Article::Type.find_by!(name: "note"), sender: Ticket::Article::Sender.find_by!(name: "Customer"), body: "Fictional.", internal: false, content_type: "text/plain")\n' if with_ticket else '')
                    + 'puts "SWOOVR=" + {id: u.id}.to_json')
    # As account deletion's final transaction leaves it (D337): mapped, deactivated, retirement requested.
    sql(f"INSERT INTO ticketing_identity (account_id, provider_user_id, state, created_at, updated_at, deactivated_at, retirement_requested_at, retirement_attempts) "
        f"VALUES ('{account}', {created['id']}, 'Applied', now(), now(), now(), now(), 0)")
    return account, created['id'], login


def main():
    general = ticket(f'Retention {RUN} general', 'General Support', f'fictional.retention.{RUN}@example.test')
    legal = ticket(f'Retention {RUN} legal', 'Legal & Law Enforcement', f'fictional.retention.legal.{RUN}@example.test')
    linked = ticket(f'Retention {RUN} linked', 'General Support', f'fictional.retention.linked.{RUN}@example.test')
    reopened = ticket(f'Retention {RUN} reopened', 'General Support', f'fictional.retention.reopened.{RUN}@example.test',
                      after='t.update!(state: Ticket::State.find_by!(name: "open"))')
    # A fictional open case about the fixture member (closed again at the end), so the linked-case rule is exercised.
    open_case = str(uuid.uuid4())
    sql(f"INSERT INTO moderation_cases (id, subject_account_id, status, queue, priority, created_at, updated_at) "
        f"SELECT '{open_case}', id, 'Open', 'Support', 'Normal', now(), now() FROM accounts WHERE email = 'fictional.alex@example.test' LIMIT 1")
    if not sql(f"SELECT 1 FROM moderation_cases WHERE id = '{open_case}'"): open_case = None
    if open_case:
        sql(f"INSERT INTO ticketing_case_link (id, provider_ticket_id, case_id, appeal_id, linked_by, linked_at) VALUES ('{uuid.uuid4()}', {linked['id']}, '{open_case}', NULL, '{uuid.uuid4()}', now())")
    retired_account, retired_user, _ = retiree(with_ticket=False)
    kept_account, kept_user, kept_login = retiree(with_ticket=True)

    purge()
    row = lambda t: sql(f"SELECT state || '|' || coalesce(to_char(retain_until AT TIME ZONE 'UTC','YYYY-MM-DD'),'') || '|' || coalesce(failure_code,'') || '|' || category FROM helpdesk_ticket_retention WHERE provider_ticket_id = {t['id']}")
    check(row(general).startswith('Disposing|'), 'the expired General Support ticket is being deleted through a data privacy task')
    legal_row = row(legal).split('|')
    check(legal_row[0] == 'Retained' and legal_row[3] == 'Legal' and int(legal_row[1][:4]) >= 2030, 'the Legal & Law Enforcement ticket is kept for 7 years')
    if open_case: check(row(linked).split('|')[2] == 'open-case' and exists(linked['id']), 'a ticket linked to an open case is kept')
    else: print('SKIP: no open moderation case in the local database for the linked-case check')
    reopened_row = row(reopened).split('|')
    check(reopened_row[0] == 'Open' and reopened_row[1] == '', 'a reopened ticket is tracked as open with no end date')
    retired = user(retired_user)
    check(retired and retired['login'].startswith('retired-') and len(retired['login']) == 40 and not retired['active'] and retired['email'] == ''
          and f"{retired['firstname']} {retired['lastname']}" == 'Retired Swoovr Member' and str(retired_account) not in retired['login'],
          'a finally deleted member is retired: inactive, opaque retired- login, name and email cleared')
    check(sql(f"SELECT count(*) FROM audit_entries WHERE action = 'helpdesk.customer.retired' AND target_id = '{retired_account}'") == '1', 'the retirement is audited')

    process_privacy_tasks()
    purge()
    check(not exists(general['id']) and row(general).startswith('Disposed|'), 'the expired ticket is deleted from Zammad and recorded as disposed')
    check(sql(f"SELECT count(*) FROM audit_entries WHERE action = 'helpdesk.ticket.deleted' AND target_id = '{general['id']}'") == '1', 'the deletion is audited (IDs only)')
    process_privacy_tasks()
    purge()
    check(user(general['customer']) is None, 'its email-only customer is deleted once no tickets remain')
    check(user(retired_user) is None and sql(f"SELECT customer_deleted_at IS NOT NULL FROM ticketing_identity WHERE account_id = '{retired_account}'") == 't',
          'the retired customer with no tickets is deleted')
    kept = user(kept_user)
    check(kept and kept['login'].startswith('retired-') and not kept['active'], 'a retired customer with a remaining ticket is kept, retired')

    # Simulated restore: the customer is back as it was, and a disposed ticket is back in Zammad.
    restored = ticket(f'Retention {RUN} restored', 'General Support', f'fictional.retention.restored.{RUN}@example.test')
    sql(f"INSERT INTO helpdesk_ticket_retention (provider_ticket_id, provider_customer_id, group_id, category, state, first_seen_at, last_reviewed_at, disposed_at, attempts) "
        f"VALUES ({restored['id']}, {restored['customer']}, 2, 'General', 'Disposed', now(), now(), now(), 0)")
    rails(f'User.find({int(kept_user)}).update!(login: "{kept_login}", active: true, email: "fictional.reverted.{RUN}@example.test")\nputs "SWOOVR=" + {{ok: true}}.to_json')
    code, out = cli('retention', 'helpdesk', '--reconcile')
    check(code == 0 and 'Helpdesk reconciled' in out, 'reconcile ran: ' + out.strip().splitlines()[-1])
    again = user(kept_user)
    check(again['login'].startswith('retired-') and not again['active'] and again['email'] == '', 'after a restore, the reverted customer is retired again')
    process_privacy_tasks()
    purge()
    check(not exists(restored['id']), 'after a restore, an already-disposed ticket is deleted again')
    if open_case:
        # Closing the case lets the linked ticket go at the next due check (reassessed daily, not deleted on release).
        sql(f"UPDATE moderation_cases SET status = 'Closed', updated_at = now() WHERE id = '{open_case}'")
        sql(f"UPDATE helpdesk_ticket_retention SET next_attempt_at = now() WHERE provider_ticket_id = {linked['id']}")
        purge(); process_privacy_tasks(); purge()
        check(not exists(linked['id']), 'once the linked case closes, the ticket is deleted at its next due check')
    print(f'PASS helpdesk retention: {checks} live checks (local Zammad and API; fictional data only).')


if __name__ == '__main__':
    main()
