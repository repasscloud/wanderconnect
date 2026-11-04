# Explicit local operator provisioning, never called by runtime HTTP handlers.
require 'json'
input=JSON.parse(STDIN.read)
raise 'Unexpected Zammad version' unless Version.get.start_with?('7.2.2')
actor=User.find_by!(login:'swoovr-provisioner').id
UserInfo.current_user_id=actor
ordinary=['General Support','Billing & Subscriptions','Account Recovery','Appeals','Public Relations & Media','Partnerships & Business Development','Marketing & Community']
staff={}
input.fetch('staff').each do |record|
  id=record.fetch('id')
  raise 'Invalid staff key' unless id.match?(/\A[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}\z/)
  roles=record.fetch('roles')
  grants={}
  if (roles & ['Support','TrustSafety','Admin']).any?
    ordinary.each {|group|grants[group]=['full']}
    grants['Trust & Safety — Restricted']=['create']
  end
  if roles.include?('TrustSafety')
    grants['Trust & Safety']=['full']
    grants['Trust & Safety — Restricted']=['full']
  end
  if roles.include?('Admin')
    ['Trust & Safety','Privacy & Data Protection','Internal Operations','Security'].each {|group|grants[group]=['full']}
  end
  if roles.include?('Legal')
    ['Legal & Law Enforcement','Privacy & Data Protection'].each {|group|grants[group]=['full']}
  end
  next if grants.empty?
  login='swoovr-staff-'+id
  user=User.find_or_initialize_by(login:login)
  user.assign_attributes(firstname:'Swoovr',lastname:'Staff',email:nil,active:true,role_ids:[Role.find_by!(name:'Agent').id],created_by_id:actor,updated_by_id:actor)
  user.save!
  user.group_names_access_map=grants
  token=Token.find_by(user_id:user.id,action:'api',name:'swoovr-runtime-staff')||Token.create!(user_id:user.id,action:'api',name:'swoovr-runtime-staff',persistent:true,preferences:{permission:['ticket.agent']})
  staff[id]={Token:token.token}
end
# Revoked/deleted local staff identities lose provider access on explicit reprovisioning as well as API denial.
User.where("login LIKE 'swoovr-staff-%'").find_each do |user|
  next unless input['reconcile']
  next if staff.key?(user.login.delete_prefix('swoovr-staff-'))
  user.update!(active:false)
end
puts 'SWOOVR_RUNTIME_STAFF='+JSON.generate(staff)
