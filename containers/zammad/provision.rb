# DEFAULT local bootstrap through the officially documented Rails runner. Pinned to 7.2.2.
# Input arrives on stdin; the caller captures stdout privately because token material is returned.
require 'json'
input = JSON.parse(STDIN.read)
raise 'Unexpected Zammad version' unless Version.get.start_with?('7.2.2')
UserInfo.current_user_id = User.find_by!(login: '-').id
actor = UserInfo.current_user_id
names = [
  'General Support', 'Billing & Subscriptions', 'Account Recovery', 'Appeals',
  'Trust & Safety', 'Trust & Safety — Restricted', 'Legal & Law Enforcement',
  'Privacy & Data Protection', 'Public Relations & Media', 'Partnerships & Business Development',
  'Marketing & Community', 'Internal Operations', 'Security',
  # D349, #281: the scheduled Home City check's internal-note items (Support, Trust & Safety and Admin).
  'Home City checks'
]
groups = names.to_h do |name|
  group = Group.find_or_initialize_by(name: name)
  group.assign_attributes(active: true, follow_up_possible: 'yes', created_by_id: actor, updated_by_id: actor)
  group.save!
  [name, group.id]
end
# Preserve historic tickets and references; the stock Users queue is disabled only when empty.
stock = Group.find_by(name: 'Users')
stock.update!(active: false) if stock && !Ticket.exists?(group_id: stock.id)
admin_role = Role.find_by!(name: 'Admin')
agent_role = Role.find_by!(name: 'Agent')
customer_role = Role.find_by!(name: 'Customer')
admin = User.find_or_initialize_by(login: 'swoovr-provisioner')
admin.assign_attributes(firstname: 'Swoovr', lastname: 'Provisioner', email: 'provisioner@example.test',
  active: true, password: input.fetch('password'), role_ids: [admin_role.id, agent_role.id],
  created_by_id: actor, updated_by_id: actor)
admin.save!
admin.group_ids_access_map = groups.values.to_h { |id| [id, ['full']] }
# Provisioning identity is never used as the runtime ticket token.
identity_role = Role.find_or_initialize_by(name: 'Swoovr identity provisioning')
identity_role.assign_attributes(active: true, default_at_signup: false, created_by_id: actor, updated_by_id: actor)
identity_role.save!
identity_role.permission_grant('admin.user')
identity = User.find_or_initialize_by(login: 'swoovr-identity')
identity.assign_attributes(firstname: 'Swoovr', lastname: 'Identity', email: 'identity@example.test', active: true,
  password: input.fetch('password'), role_ids: [identity_role.id], created_by_id: actor, updated_by_id: actor)
identity.save!
# Staff directory (Q133/#204): Swoovr mirrors its staff permissions into Zammad through this identity only. It can manage
# users and sessions; ticket.agent only lets it read the group and role lists, because it belongs to no group and so
# sees no tickets. Swoovr always writes agents with exactly the Agent role.
directory_role = Role.find_or_initialize_by(name: 'Swoovr staff directory')
directory_role.assign_attributes(active: true, default_at_signup: false, created_by_id: actor, updated_by_id: actor)
directory_role.save!
%w[admin.user admin.session ticket.agent].each { |permission| directory_role.permission_grant(permission) }
directory = User.find_or_initialize_by(login: 'swoovr-directory')
directory.assign_attributes(firstname: 'Swoovr', lastname: 'Staff directory', email: 'directory@example.test', active: true,
  password: input.fetch('password'), role_ids: [directory_role.id], created_by_id: actor, updated_by_id: actor)
directory.save!
directory.group_names_access_map = {}
# Retention (D337, #224): reads ticket metadata in every group and deletes only through data privacy tasks. It belongs to
# no Agent role, so it is never an assignable owner and staff synchronisation never manages it.
retention_role = Role.find_or_initialize_by(name: 'Swoovr retention')
retention_role.assign_attributes(active: true, default_at_signup: false, created_by_id: actor, updated_by_id: actor)
retention_role.save!
%w[admin.data_privacy ticket.agent].each { |permission| retention_role.permission_grant(permission) }
retention = User.find_or_initialize_by(login: 'swoovr-retention')
retention.assign_attributes(firstname: 'Swoovr', lastname: 'Retention', email: 'retention@example.test', active: true,
  password: input.fetch('password'), role_ids: [retention_role.id], created_by_id: actor, updated_by_id: actor)
retention.save!
retention.group_ids_access_map = Group.pluck(:id).to_h { |id| [id, ['read']] }
# Home City check (D349, #281): raises the scheduled check's items as an internal note and reads their state for the
# 90-day cooldown, in "Home City checks" only. Like retention, it has no Agent role, so it is never an assignable owner and
# staff synchronisation never manages it.
home_city_role = Role.find_or_initialize_by(name: 'Swoovr Home City check')
home_city_role.assign_attributes(active: true, default_at_signup: false, created_by_id: actor, updated_by_id: actor)
home_city_role.save!
home_city_role.permission_grant('ticket.agent')
home_city = User.find_or_initialize_by(login: 'swoovr-home-city-check')
home_city.assign_attributes(firstname: 'Swoovr', lastname: 'Home City check', email: 'home-city-check@example.test', active: true,
  password: input.fetch('password'), role_ids: [home_city_role.id], created_by_id: actor, updated_by_id: actor)
home_city.save!
home_city.group_ids_access_map = {groups.fetch('Home City checks') => ['create', 'read']}
def api_token(user, label, permissions)
  token = Token.find_by(action: 'api', user_id: user.id, name: label)
  token ||= Token.create!(action: 'api', persistent: true, user_id: user.id, name: label,
    preferences: {permission: permissions})
  token.update!(preferences: {permission: permissions})
  token.token
end
Setting.set('system_init_done', true)
Setting.set('api_token_access', true)
Setting.set('api_password_access', false)
Setting.set('user_create_account', false)
# Staff sign in to Zammad's web interface only through the central staff identity provider (Keycloak OpenID Connect).
# Password sign-in and password reset are off, so a Zammad password can never replace the central sign-in. Unknown
# identities are never created; a staff web identity provisioned by Swoovr is linked by its verified email on first use.
sso = input.fetch('sso', nil)
if sso
  Setting.set('auth_openid_connect', true)
  Setting.set('auth_openid_connect_credentials', {
    'display_name' => sso.fetch('display_name'), 'identifier' => sso.fetch('client_id'), 'issuer' => sso.fetch('issuer'),
    'uid_field' => 'sub', 'scope' => 'openid email profile', 'pkce' => true })
  Setting.set('auth_third_party_no_create_user', true)
  Setting.set('auth_third_party_auto_link_at_inital_login', true)
end
Setting.set('user_show_password_login', sso.nil?)
Setting.set('user_lost_password', false)
# Local outgoing email never leaves the Docker mail sink. A hosted run (deploy/vultr-uat) may pass its own
# notification_smtp; without it the local sink is kept, so local behaviour is unchanged.
smtp = {'host' => 'swoovr-platform', 'port' => 1025, 'ssl' => false, 'enable_starttls_auto' => false, 'user' => '', 'password' => ''}
  .merge(input.fetch('notification_smtp', {}))
Channel.where(area: 'Email::Notification').update_all(active: false)
notification = Channel.find_by(area: 'Email::Notification') || Channel.new(area: 'Email::Notification')
notification.assign_attributes(options: {outbound: {adapter: 'smtp', options: {host: smtp.fetch('host'), port: Integer(smtp.fetch('port')),
  ssl: smtp.fetch('ssl') == true, enable_starttls_auto: smtp.fetch('enable_starttls_auto') == true, user: smtp.fetch('user'),
  password: smtp.fetch('password')}}}, active: true, preferences: {online_service_disable: true},
  created_by_id: actor, updated_by_id: actor)
notification.save!

# Minimal ticket attributes: references/classification, never original evidence or identity documents.
{
  'swoovr_operation' => ['Swoovr operation', 36],
  'swoovr_case' => ['Swoovr case reference', 80],
  'swoovr_category' => ['Category', 80],
  'swoovr_subcategory' => ['Subcategory', 80],
  'swoovr_sensitivity' => ['Sensitivity', 40],
  'swoovr_resolution' => ['Resolution', 80],
  'swoovr_source' => ['Source', 40],
}.each do |name, (display, maximum)|
  next if ObjectManager::Attribute.exists?(object_lookup_id: ObjectLookup.by_name('Ticket'), name: name)
  ObjectManager::Attribute.add(object: 'Ticket', name: name, display: display, data_type: 'input',
    data_option: {type: 'text', maxlength: maximum, null: true}, editable: true, active: true,
    screens: {create_middle: {'ticket.agent' => {null: true}}, edit: {'ticket.agent' => {null: true}}},
    position: 1000, created_by_id: actor, updated_by_id: actor)
end
ObjectManager::Attribute.migration_execute if ObjectManager::Attribute.pending_migration?
# DEFAULT ordinary correspondence templates. Operators may author further permission-scoped templates.
text = TextModule.find_or_initialize_by(name: 'Swoovr support acknowledgement')
text.assign_attributes(content: 'Thank you for contacting Swoovr support. We will review your request.', active: true,
  created_by_id: actor, updated_by_id: actor)
text.save!
# Issue 222 (owner, D331): canned replies, named "Group · Response" so Swoovr Admin groups them. Re-running updates the text.
{
  'General support' => {
    'Request received' => 'Thank you for contacting Swoovr Support. We have received your request and will review it.',
    'Under review' => 'Your request is currently being reviewed by our support team. We will provide an update when more information is available.',
    'Additional information required' => 'We need some additional information to assist with your request. Please reply with the requested details so we can continue.',
    'Request updated' => 'Your support request has been updated. Please review the latest response for further information.',
    'Escalated' => 'Your request has been referred to the appropriate team for further review.',
    'Delayed response' => 'Your request is taking longer than expected to review. Thank you for your patience.'
  },
  'Account and access' => {
    'Account access' => 'We have received your account access request and will review the information provided.',
    'Password reset' => 'Please use the password reset option on the Swoovr sign-in page. If you continue to experience difficulties, reply to this request.',
    'MFA assistance' => 'We have received your request for assistance with multi-factor authentication. Additional identity verification may be required before changes can be made.',
    'Account restriction' => 'Your request regarding an account restriction has been received and will be reviewed.',
    'Account deletion' => 'We have received your account deletion enquiry. We will review your request and provide information about the next steps.',
    'Account details' => 'We have received your request to update your account information. Additional verification may be required.'
  },
  'Trust & Safety' => {
    'Report received' => 'Thank you for bringing this matter to our attention. Your report has been received and will be reviewed in accordance with our Community Guidelines.',
    'Safety review' => 'Your report has been referred to our Trust & Safety team for further assessment.',
    'Harassment report' => 'We have received your report concerning harassment or inappropriate behaviour. Our Trust & Safety team will review the information provided.',
    'Impersonation' => 'We have received your impersonation report. Additional information may be requested to assist our investigation.',
    'Content report' => "Thank you for reporting this content. We will assess it against Swoovr's Community Guidelines.",
    'Appeal received' => 'Your appeal has been received and will be reviewed. We will notify you when a decision has been made.',
    'Investigation completed' => 'Our review of your report has been completed. For privacy and safety reasons, we may not be able to disclose any action taken against another account.'
  },
  'Identity and photo verification' => {
    'Verification assistance' => 'We have received your verification support request and will review the issue.',
    'Verification unsuccessful' => 'We understand you have experienced difficulties completing verification. Our team will review the information available and advise on the next steps.',
    'Photo moderation' => 'Your enquiry regarding photo moderation has been received. We will review the matter against our content requirements.',
    'Identity verification' => 'Your identity verification enquiry has been received. Additional verification steps may be required to protect your account.'
  },
  'Billing and subscriptions' => {
    'Billing enquiry' => 'We have received your billing enquiry and will review the details provided.',
    'Refund request' => 'Your refund request has been received and will be assessed against the applicable payment terms and refund requirements.',
    'Subscription cancellation' => 'We have received your enquiry regarding subscription cancellation. We will review your request and provide further information.',
    'Payment issue' => 'We have received your report of a payment issue and will investigate the details.',
    'Unrecognised charge' => 'We have received your report of an unrecognised charge. We may request additional transaction information to assist our investigation.'
  },
  'Business partners' => {
    'Partnership enquiry' => 'Thank you for your interest in partnering with Swoovr. We have received your enquiry and will review the information provided.',
    'Existing partner support' => 'Your business partner support request has been received and will be reviewed by the appropriate team.',
    'Partner payments' => 'We have received your enquiry regarding partner payments or settlements and will review the relevant transaction details.',
    'Offer or redemption issue' => 'We have received your report concerning a partner offer or redemption. We will review the issue and provide an update.'
  },
  'Resolution and closure' => {
    'Resolved' => 'Your support request has been resolved. If you require further assistance, please contact Swoovr Support.',
    'Closed — no response' => 'We have closed this request as we have not received the additional information required to proceed. You may submit a new request if you still require assistance.',
    'Closed — no further action' => 'Our review of your request is complete, and no further action is required at this time.',
    'Closed — unable to proceed' => 'We are unable to proceed with your request based on the information currently available. You may submit a new request with additional details.',
    'Duplicate request' => 'This request has been closed because it duplicates an existing support request. Please refer to your original request for further updates.',
    'Closed — referred elsewhere' => 'This request has been closed following referral to the appropriate team or service. Any further communication will be provided through the relevant channel.'
  },
  'System and technical issues' => {
    'Technical issue received' => 'We have received your report of a technical issue and will investigate.',
    'Known issue' => 'The issue you reported is known to our technical team. We are working to address it and will provide updates when available.',
    'Unable to reproduce' => 'We have been unable to reproduce the reported issue. Please provide any additional details, screenshots or steps that may assist our investigation.',
    'Issue resolved' => 'The technical issue you reported has been addressed. Please check whether the problem is resolved on your device.',
    'Feature suggestion' => 'Thank you for your suggestion. Your feedback has been recorded for consideration in future product development.'
  }
}.each do |group, replies|
  replies.each do |label, content|
    module_row = TextModule.find_or_initialize_by(name: "#{group} · #{label}")
    module_row.assign_attributes(content: content, active: true, created_by_id: actor, updated_by_id: actor)
    module_row.save!
  end
end
# D349, #281: Support's friendly suggestion, as a text module for Swoovr Admin replies and a Zammad macro (a public note).
home_city_reply = "Hi, we're checking in because it looks like you may have moved recently. If your Home City has changed, you can update it any time in Me → Profile, so people near you can find you. If nothing has changed, there's nothing you need to do. Thanks for being part of Swoovr."
home_city_text = TextModule.find_or_initialize_by(name: 'Home City checks · Suggest updating your Home City')
home_city_text.assign_attributes(content: home_city_reply, active: true, created_by_id: actor, updated_by_id: actor)
home_city_text.save!
home_city_macro = Macro.find_or_initialize_by(name: 'Suggest updating your Home City')
home_city_macro.assign_attributes(perform: {'article.note' => {'subject' => 'Your Home City', 'body' => home_city_reply, 'internal' => 'false'}},
  group_ids: [groups.fetch('Home City checks')], active: true, ux_flow_next_up: 'none', created_by_id: actor, updated_by_id: actor)
home_city_macro.save!
macro = Macro.find_or_initialize_by(name: 'Swoovr close ticket')
macro.assign_attributes(perform: {'ticket.state_id' => {'value' => Ticket::State.find_by!(name: 'closed').id}},
  active: true, ux_flow_next_up: 'none', created_by_id: actor, updated_by_id: actor)
macro.save!
# New support mail (any sender) starts in General Support, never the disabled stock Users group. Follow-ups keep their queue.
general = Group.find_by!(name: 'General Support')
mail_filter = PostmasterFilter.find_or_initialize_by(name: 'Swoovr: new support mail to General Support')
mail_filter.assign_attributes(channel: 'email', active: true,
  match: {'from' => {'operator' => 'contains', 'value' => '@'}},
  perform: {'X-Zammad-Ticket-group_id' => {'value' => general.id.to_s}},
  note: 'Provisioned by Swoovr', created_by_id: actor, updated_by_id: actor)
mail_filter.save!
# Webhook/trigger stay inactive unless provisioning explicitly enables events (zammad_local.py provision --events).
events = input.fetch('events_active', false) == true
webhook = Webhook.find_or_initialize_by(name: 'Swoovr events')
# Hosted runs pass the API endpoint (deploy/vultr-uat: https://api-uat.swoovr.app/..., via Caddy); local default unchanged.
webhook.assign_attributes(endpoint: input.fetch('webhook_endpoint', 'http://swoovr-platform:5080/api/integrations/zammad/events'),
  signature_token: input.fetch('webhook_secret'), active: events, ssl_verify: true,
  customized_payload: true, custom_payload: JSON.generate({ticket: {id: '#{ticket.id}', updated_at: '#{ticket.updated_at}', customer_id: '#{ticket.customer_id}', state_id: '#{ticket.state_id}', group_id: '#{ticket.group_id}', owner_id: '#{ticket.owner_id}', priority_id: '#{ticket.priority_id}'}, article: {id: '#{article.id}'}}),
  created_by_id: actor, updated_by_id: actor)
webhook.save!
trigger = Trigger.find_or_initialize_by(name: 'Swoovr correspondence events')
trigger.assign_attributes(condition: {'ticket.state_id' => {'operator' => 'is', 'value' => Ticket::State.pluck(:id)}},
  perform: {'notification.webhook' => {'webhook_id' => webhook.id.to_s}}, activator: 'action',
  execution_condition_mode: 'always', active: events, created_by_id: actor, updated_by_id: actor)
trigger.save!
puts "SWOOVR_PROVISION_RESULT=" + JSON.generate({version: Version.get, groups: groups,
  customer_role_id: customer_role.id, agent_role_id: agent_role.id,
  provisioning_token: api_token(admin, 'swoovr-local-provisioning', ['admin', 'ticket.agent']),
  identity_token: api_token(identity, 'swoovr-local-identity', ['admin.user']),
  staff_directory_token: api_token(directory, 'swoovr-local-staff-directory', ['admin.user', 'admin.session', 'ticket.agent']),
  retention_token: api_token(retention, 'swoovr-local-retention', ['admin.data_privacy', 'ticket.agent']),
  home_city_check_token: api_token(home_city, 'swoovr-local-home-city-check', ['ticket.agent'])})
