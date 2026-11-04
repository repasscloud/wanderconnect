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
  'Marketing & Community', 'Internal Operations', 'Security'
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
# Local outgoing email never leaves the Docker mail sink.
Channel.where(area: 'Email::Notification').update_all(active: false)
notification = Channel.find_by(area: 'Email::Notification') || Channel.new(area: 'Email::Notification')
notification.assign_attributes(options: {outbound: {adapter: 'smtp', options: {host: 'swoovr-platform', port: 1025,
  ssl: false, enable_starttls_auto: false, user: '', password: ''}}}, active: true, preferences: {online_service_disable: true},
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
webhook.assign_attributes(endpoint: 'http://swoovr-platform:5080/api/integrations/zammad/events',
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
  identity_token: api_token(identity, 'swoovr-local-identity', ['admin.user'])})
