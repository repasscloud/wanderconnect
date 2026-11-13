#!/usr/bin/env bash
# Send ONE plain test email from the UAT server through the same provider, sender and key that Keycloak and the API
# use (Twilio Email API, env/mail.env). Run it yourself, on your Mac. It proves the server can hand mail to Twilio;
# delivery to the mailbox is then up to Twilio and the receiving provider (check the inbox AND junk folder).
#
#   scripts/uat/mail-test.sh <server IPv4> <recipient address>
#
# SSH uses the dedicated deploy key ($SWOOVR_UAT_SSH_KEY, default ~/.ssh/swoovr-uat-deploy). Nothing is changed on the
# server. Output: the configured service names, Twilio's HTTP status and whether it returned an operation ID; never a
# key or secret. See docs/delivery/UAT-OPERATIONS-SOP.md "Email".
set -euo pipefail
host="${1:?usage: mail-test.sh <server IPv4> <recipient address>}"
to="${2:?usage: mail-test.sh <server IPv4> <recipient address>}"
[[ "$to" =~ ^[^@[:space:]\']+@[^@[:space:]\']+\.[^@[:space:]\']+$ ]] || { echo "Not an email address: $to" >&2; exit 2; }
key="${SWOOVR_UAT_SSH_KEY:-$HOME/.ssh/swoovr-uat-deploy}"
ssh -i "$key" -o IdentitiesOnly=yes -o BatchMode=yes -o ConnectTimeout=15 "root@$host" \
  "cd /opt/swoovr/swoovr-uat && python3 -I - env/mail.env '$to'" <<'PY'
import base64, json, sys, urllib.error, urllib.request
values = {}
for line in open(sys.argv[1], encoding='utf-8'):
    key, _, value = line.rstrip('\n').partition('=')
    values[key] = value.strip('"\'')
services = {name: values.get('SWOOVR_' + name) for name in ('KEYCLOAK_MAIL_SERVICE', 'API_MAIL_SERVICE')}
print('Keycloak mail service:', services['KEYCLOAK_MAIL_SERVICE'], '| API mail service:', services['API_MAIL_SERVICE'],
      '| from:', values.get('SWOOVR_MAIL_FROM'))
if 'twilio' not in services.values():
    sys.exit('Neither service is twilio; this test only covers the Twilio Email API.')
auth = 'Basic ' + base64.b64encode((values['SWOOVR_MAIL_TWILIO_KEY_SID'] + ':' + values['SWOOVR_MAIL_TWILIO_KEY_SECRET']).encode()).decode()
text = 'Test of the Swoovr sender (Twilio Email API). No action needed.'
message = {'from': {'address': values['SWOOVR_MAIL_FROM'], 'name': values.get('SWOOVR_MAIL_FROM_NAME') or 'Swoovr'},
           'to': [{'address': sys.argv[2]}],
           'content': {'subject': 'Swoovr mail delivery test', 'text': text,
                       'html': '<html><body><p>' + text + '</p></body></html>'}}
request = urllib.request.Request('https://comms.twilio.com/v1/Emails', method='POST', data=json.dumps(message).encode(),
                                 headers={'Authorization': auth, 'Content-Type': 'application/json', 'Accept': 'application/json'})
try:
    with urllib.request.urlopen(request, timeout=20) as response:
        code, body = response.status, json.loads(response.read() or b'{}')
except urllib.error.HTTPError as error:
    code, body = error.code, {}
    try:
        body = json.loads(error.read() or b'{}')
    except ValueError:
        pass
if code == 202 and body.get('operationId'):
    print('ACCEPTED by Twilio (HTTP 202, operation ID returned). Now check the inbox and junk folder.')
else:
    print('REFUSED by Twilio: HTTP', code, json.dumps(body.get('errors') or body.get('message'))[:600])
    sys.exit(1)
PY
