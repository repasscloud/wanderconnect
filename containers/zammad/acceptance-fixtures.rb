# Fictional adults, only for explicitly requested local acceptance. Never runs on normal startup.
require 'json'
raise 'Unexpected Zammad version' unless Version.get.start_with?('7.2.2')
actor = User.find_by!(login: 'swoovr-provisioner').id
UserInfo.current_user_id = actor
rows = {}
{
  'customer_a' => ['swoovr-aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', 'Customer', {}],
  'customer_b' => ['swoovr-bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb', 'Customer', {}],
  'support' => ['swoovr-fixture-support', 'Agent', {'General Support' => ['full'], 'Trust & Safety — Restricted' => ['create']}],
  'restricted' => ['swoovr-fixture-restricted', 'Agent', {'Trust & Safety — Restricted' => ['full']}],
}.each do |key, (login, role, groups)|
  user = User.find_or_initialize_by(login: login)
  user.assign_attributes(firstname: 'Fictional', lastname: key, email: "#{key}@example.test", active: true,
    role_ids: [Role.find_by!(name: role).id], created_by_id: actor, updated_by_id: actor)
  user.save!
  user.group_names_access_map = groups if role == 'Agent'
  permission = role == 'Agent' ? 'ticket.agent' : 'ticket.customer'
  token = Token.find_by(user_id: user.id, action: 'api', name: 'swoovr-acceptance') || Token.create!(user_id: user.id,
    action: 'api', name: 'swoovr-acceptance', persistent: true, preferences: {permission: [permission]})
  rows[key] = {id: user.id, login: login, token: token.token}
end
puts 'SWOOVR_ACCEPTANCE_FIXTURES=' + JSON.generate(rows)
