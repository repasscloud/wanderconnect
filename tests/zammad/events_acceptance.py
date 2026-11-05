#!/usr/bin/env python3
"""Helpdesk event reliability against the running local platform and real local Zammad (events enabled with
`zammad_local.py provision --events`). Real faults: Zammad's nginx is stopped (provider outage), then the Swoovr API
container is stopped (missed delivery). Each public staff reply must produce exactly one member notice, and a private note
none. Fictional fixture member only; restores every container it stops. Prints ids and counts only.
"""
import json, subprocess, time, uuid
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]

def sql(query):
    return subprocess.run(['docker', 'exec', 'swoovr-platform-postgres-1', 'psql', '-p', '5433', '-U', 'swoovr', '-d', 'swoovr', '-At', '-c', query],
                          check=True, capture_output=True, text=True).stdout.strip()

def rails(code):
    out = subprocess.run(['docker', 'exec', 'swoovr-zammad-zammad-railsserver-1', 'bundle', 'exec', 'rails', 'runner', code], capture_output=True, text=True)
    lines = [l for l in out.stdout.splitlines() if l.startswith('SWOOVR=')]
    if out.returncode or not lines: raise RuntimeError('Rails fixture step failed')
    return json.loads(lines[-1][7:])

def wait(predicate, seconds, label):
    end = time.time() + seconds
    while time.time() < end:
        if predicate(): return True
        time.sleep(3)
    raise RuntimeError('FAIL: ' + label)

def article(ticket, internal, run):
    # A staff member's reply written inside the helpdesk itself (as an agent would in Zammad), firing the real trigger.
    return rails(f'''t=Ticket.find({ticket}); u=User.where("login LIKE 'swoovr-staff-%' AND active = true").find {{|x| x.group_access?(t.group_id, "full") }}
UserInfo.current_user_id = u.id
a = nil
Transaction.execute(interface_handle: "application_server") do
a=Ticket::Article.create!(ticket_id: t.id, body: "Fictional {'note' if internal else 'reply'} {run}", content_type: "text/plain", internal: {str(internal).lower()},
  sender: Ticket::Article::Sender.find_by(name: "Agent"), type: Ticket::Article::Type.find_by(name: "note"), created_by_id: u.id, updated_by_id: u.id)
end
puts "SWOOVR=" + {{id: a.id}}.to_json''')['id']

def notices(article_id):
    return int(sql(f"SELECT count(*) FROM notifications WHERE kind = 'Support' AND dedupe_key = 'support-article:{article_id}'") or 0)

def main():
    run = uuid.uuid4().hex[:8]; checks = 0
    def check(ok, label):
        nonlocal checks
        if not ok: raise RuntimeError('FAIL: ' + label)
        checks += 1; print('PASS:', label, flush=True)
    customer = sql("SELECT t.provider_user_id FROM ticketing_identity t JOIN accounts a ON a.id = t.account_id WHERE a.email = 'fictional.alex@example.test' AND t.state = 'Applied'")
    ticket = rails(f'puts "SWOOVR=" + {{id: Ticket.where(customer_id: {customer}).order(:id).last.id}}.to_json')['id']
    print('Using fixture member ticket TK-%d' % ticket)
    try:
        # 1. Provider outage: Swoovr accepts the signed event, the re-fetch fails and backs off, then recovers once.
        subprocess.run(['docker', 'stop', 'swoovr-zammad-zammad-nginx-1'], check=True, capture_output=True)
        reply = article(ticket, False, run)
        wait(lambda: sql(f"SELECT count(*) FROM ticketing_webhook_work WHERE provider_article_id = {reply} AND failure_code IS NOT NULL AND completed_at IS NULL") == '1', 180, 'outage recorded as a retryable failure')
        check(notices(reply) == 0, 'during the outage the event is kept, retried with backoff, and nothing is sent')
        subprocess.run(['docker', 'start', 'swoovr-zammad-zammad-nginx-1'], check=True, capture_output=True)
        wait(lambda: notices(reply) == 1, 300, 'notice after recovery')
        check(sql(f"SELECT count(*) FROM ticketing_webhook_work WHERE provider_article_id = {reply} AND completed_at IS NOT NULL") == '1', 'after recovery the event completes and the member gets exactly one notice')
        note = article(ticket, True, run)
        time.sleep(20)
        check(notices(note) == 0, 'a private note never produces a notice')
        # 2. Swoovr API down: the delivery is missed; Zammad's own retry or Swoovr's reconciliation sweep recovers it once.
        subprocess.run(['docker', 'stop', 'swoovr-platform-api-1'], check=True, capture_output=True)
        missed = article(ticket, False, run + '-missed')
        time.sleep(10)
        subprocess.run(['docker', 'start', 'swoovr-platform-api-1'], check=True, capture_output=True)
        wait(lambda: notices(missed) == 1, 420, 'missed event recovered')
        time.sleep(150)
        check(notices(missed) == 1 and notices(reply) == 1, 'a missed delivery is recovered exactly once (retry or sweep), with no duplicate from either')
        print(f'PASS: {checks} helpdesk event reliability checks with real outages (TK-{ticket}).')
    finally:
        subprocess.run(['docker', 'start', 'swoovr-zammad-zammad-nginx-1'], capture_output=True)
        subprocess.run(['docker', 'start', 'swoovr-platform-api-1'], capture_output=True)

if __name__ == '__main__':
    main()
