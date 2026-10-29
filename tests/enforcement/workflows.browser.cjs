// Fictional local Keycloak tokens + an actual isolated API/throwaway PostgreSQL. No owner application DB,
// real registration/ownership verification, SMS/push/email delivery or external screening is claimed.
const assert=require('node:assert/strict'),crypto=require('node:crypto');
const {execFile,spawn}=require('node:child_process'),{once}=require('node:events');
const net=require('node:net'),fs=require('node:fs/promises'),path=require('node:path'),os=require('node:os');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
const {sql,localEnv,createMember,memberToken,createStaff,staffToken,rootAdminToken,claims}=require('../auth/local-tokens.cjs');
const run=(command,args,options={})=>new Promise((resolve,reject)=>execFile(command,args,options,(error,out,err)=>error?reject(new Error(command+' failed: '+err.slice(-500))):resolve(out.trim())));
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
(async()=>{
 const database='swoovr_workflows_'+crypto.randomUUID().replaceAll('-',''),folder=await fs.mkdtemp(path.join(os.tmpdir(),'swoovr-workflows-'));
 const apiDir=path.join(process.cwd(),'src/Swoovr.Api'),dll=path.join(apiDir,'bin/Debug/net10.0/Swoovr.Api.dll');
 const probe=net.createServer();await new Promise(resolve=>probe.listen(0,'127.0.0.1',resolve));
 const base='http://127.0.0.1:'+probe.address().port;await new Promise(resolve=>probe.close(resolve));
 const edge=await require('../auth/managed-edge.cjs').managedEdge(chromium),browser=edge.browser;let host,replica,created=false,checks=0;
 const query=text=>run('docker',['exec','swoovr-local-postgres-1','psql','-U','swoovr','-d',database,'-v','ON_ERROR_STOP=1','-tAc',text]);
 const equal=(actual,expected,label)=>{assert.equal(actual,expected,label);checks++;};
 const stop=async()=>{if(!host||host.exitCode!==null)return;const exited=once(host,'exit');host.kill('SIGTERM');await Promise.race([exited,sleep(4000)]);if(host.exitCode===null){host.kill('SIGKILL');await exited;}host=null;};
 try {
  await sql(`CREATE DATABASE ${database}`);created=true;
  const key=Buffer.alloc(32,31);
  const env={...process.env,DOTNET_ENVIRONMENT:'Development',ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:base,
   ConnectionStrings__Swoovr:`Host=127.0.0.1;Port=5433;Database=${database};Username=swoovr;Password=${await localEnv('SWOOVR_DB_PASSWORD')}`,
   Founders__Secrets__1:await localEnv('SWOOVR_FOUNDER_SECRET_V1'),Bans__HmacKeys__1:key.toString('base64'),Staff__LocalPasswords:'true',MemberAdmin__ClientSecret:await localEnv('SWOOVR_MEMBERS_API_ADMIN_SECRET'),
   Beta__Enabled:'false',Email__Smtp__Host:'127.0.0.1',Email__Smtp__Port:'1025',Sms__Provider:'simulated',Phone__Required:'false',Phone__CodeKey:'fictional-isolated-phone-code-key-123456',Accounts__HoldKey:'fictional-isolated-legacy-hold-key-123456',Media__Store:'local',Media__Local__Path:folder,Media__Screening__Worker:'false',Retention__Worker:'false'};
  await run('dotnet',[dll,'migrate'],{cwd:apiDir,env});
  const start=async label=>{
   const handle=await fs.open(path.join(folder,label+'.log'),'w');host=spawn('dotnet',[dll],{cwd:apiDir,env,stdio:['ignore',handle.fd,handle.fd]});await handle.close();
   for(let n=0;n<100;n++) {if(host.exitCode!==null)throw new Error('Isolated registry host exited.');try{if((await fetch(base+'/health/live')).ok)return;}catch{}await sleep(100);}throw new Error('Isolated host startup timeout.');
  };
  await start('first');

  const request=async(token,route,method='GET',body)=>{
   const response=await fetch(base+route,{method,headers:{Authorization:'Bearer '+token,'Swoovr-Version':'2026-10-28',...(body?{'Content-Type':'application/json'}:{})},...(body?{body:JSON.stringify(body)}:{})});
   let value=null;try{value=await response.json();}catch{}return{status:response.status,body:value};
  };
  const adminToken=await rootAdminToken(browser);
  equal((await request(adminToken,'/api/session')).status,200,'local fictional staff identity exists');
  await run('dotnet',[dll,'root-admin','grant',claims(adminToken).sub],{cwd:apiDir,env});
  const adminId=await query(`SELECT id FROM accounts WHERE subject='${claims(adminToken).sub}'`);
  const grant=async(token,capability)=>{const result=await request(token,'/api/admin/access/grants','POST',{capability,purpose:'Fictional restricted enforcement review'});equal(result.status,201,'explicit elevated permission '+capability);};
  await grant(adminToken,'termination');
  const member=await createMember('workflow-original'),memberJwt=await memberToken(browser,member);
  equal((await request(memberJwt,'/api/session')).status,200,'original member registered');
  const original=await query(`SELECT id FROM accounts WHERE subject='${claims(memberJwt).sub}'`);
  await query(`INSERT INTO member_phones(account_id,number,verified_at) VALUES('${original}','+61491570159',now());`);
  const terminate=(id)=>request(adminToken,`/api/admin/accounts/${id}/sanctions`,'POST',{action:'terminate',reason:'Fictional permanent safety enforcement'});
  await query(`CREATE FUNCTION fixture_reject_ban_note() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Fictional required note failure'; END $$;
   CREATE TRIGGER fixture_reject_ban_note BEFORE INSERT ON ban_case_notes FOR EACH ROW EXECUTE FUNCTION fixture_reject_ban_note();`);
  equal((await terminate(original)).status>=500,true,'required note failure is not successful termination');
  equal(await query(`SELECT count(*) FROM sanctions WHERE account_id='${original}'`),'0','note failure rolls back sanction');
  equal(await query(`SELECT count(*) FROM banned_identifiers`),'0','note failure rolls back restrictions');
  equal(await query(`SELECT count(*) FROM moderation_cases WHERE subject_account_id='${original}'`),'0','note failure rolls back created enforcement case');
  await query(`DROP TRIGGER fixture_reject_ban_note ON ban_case_notes; DROP FUNCTION fixture_reject_ban_note();`);
  equal((await terminate(original)).status,200,'actual termination commits restrictions');
  const entries=JSON.parse(await query(`SELECT json_agg(json_build_object('id',id,'type',identifier_type,'caseId',case_id)) FROM banned_identifiers`));
  equal(entries.length,2,'current verified email/mobile both restricted');
  const emailEntry=entries.find(e=>e.type==='Email'),mobileEntry=entries.find(e=>e.type==='Mobile');
  equal(await query(`SELECT count(*) FROM ban_transitions WHERE action='created'`),'2','creation transitions committed');
  equal(await query(`SELECT count(*) FROM ban_case_notes`),'2','creation case notes committed');
  equal((await request(memberJwt,'/api/me/profile')).body.code,'account-suspended','original account remains terminated');
  equal((await request(memberJwt,'/api/admin/banned-identifiers/lookup','POST',{type:'email',value:member.email,purpose:'Fictional'})).status,403,'member cannot look up registry');
  equal((await request(adminToken,'/api/admin/banned-identifiers/lookup','POST',{type:'email',value:member.email,purpose:'Fictional'})).body.code,'elevated-access-required','termination permission does not grant identifier release');
  await grant(adminToken,'identifier-release');
  const lookup=await request(adminToken,'/api/admin/banned-identifiers/lookup','POST',{type:'email',value:member.email.toUpperCase(),purpose:'Fictional reassignment review'});
  equal(lookup.status,200,'authorized normalized lookup');equal(lookup.body.length,1,'exact typed match only');
  assert.ok(!JSON.stringify(lookup.body).includes(member.email)&&!JSON.stringify(lookup.body).includes('identifierHash'));checks++;
  const detail=await request(adminToken,'/api/admin/banned-identifiers/'+emailEntry.id);
  equal(detail.status,200,'authorized detail');equal(detail.body.notes[0].caseId,emailEntry.caseId,'note belongs to associated case');
  equal((await request(adminToken,'/api/admin/banned-identifiers/release','POST',{identifierIds:[emailEntry.id],reason:' '})).status,400,'mandatory explanation');
  const other=await createStaff('workflow-conflicted'),otherJwt=await staffToken(browser,other);
  equal((await request(adminToken,'/api/admin/invitations','POST',{email:other.email,role:'admin'})).status,201,'invite another fictional admin');
  await request(otherJwt,'/api/session');await grant(otherJwt,'identifier-release');
  const otherId=await query(`SELECT id FROM accounts WHERE subject='${claims(otherJwt).sub}'`);
  equal((await request(adminToken,`/api/admin/staff-links/${otherId}`,'PUT',{memberAccountId:original})).status,204,'explicit personal association');
  equal((await request(otherJwt,'/api/admin/banned-identifiers/'+emailEntry.id)).status,403,'personal conflict refuses registry detail');
  equal((await request(otherJwt,'/api/admin/cases/'+emailEntry.caseId)).status,403,'associated case review cannot bypass personal conflict');
  equal((await request(otherJwt,'/api/admin/cases?status=all')).body.some(c=>c.id===emailEntry.caseId),false,'conflicted source case is absent from queue');
  const reviewer=await createStaff('workflow-reporter-reviewer'),reviewerJwt=await staffToken(browser,reviewer);
  equal((await request(adminToken,'/api/admin/invitations','POST',{email:reviewer.email,role:'admin'})).status,201,'invite independent reporter-associated staff');
  await request(reviewerJwt,'/api/session');const reviewerId=await query(`SELECT id FROM accounts WHERE subject='${claims(reviewerJwt).sub}'`);

  const visibleMember=await createMember('workflow-visible-case'),visibleJwt=await memberToken(browser,visibleMember);
  await request(visibleJwt,'/api/session');const visibleId=await query(`SELECT id FROM accounts WHERE subject='${claims(visibleJwt).sub}'`),visibleCase=crypto.randomUUID();
  await query(`INSERT INTO moderation_cases(id,subject_account_id,status,queue,priority,created_at,updated_at)
   SELECT gen_random_uuid(),'${original}','Closed','Admin','High',now()-interval '2 days',now()-interval '2 days' FROM generate_series(1,201);
   INSERT INTO moderation_cases(id,subject_account_id,status,queue,priority,created_at,updated_at) VALUES('${visibleCase}','${visibleId}','Closed','Admin','High',now(),now());`);
  equal((await request(otherJwt,'/api/admin/cases?status=all')).body.some(c=>c.id===visibleCase),true,'conflicted queue prefix cannot starve later allowed cases');
  equal((await request(adminToken,`/api/admin/staff-links/${reviewerId}`,'PUT',{memberAccountId:visibleId})).status,204,'explicit reporter-personal association');
  const siblingCase=crypto.randomUUID(),reportId=crypto.randomUUID();
  await query(`INSERT INTO reports(id,reporter_account_id,subject_account_id,subject_type,subject_ref,category_id,priority,text,case_id,created_at)
   VALUES('${reportId}','${visibleId}','${original}','Profile','${original}',(SELECT id FROM report_categories LIMIT 1),'High','Fictional reporter statement for restricted case.','${emailEntry.caseId}',now());
   INSERT INTO moderation_cases(id,subject_account_id,status,queue,priority,created_at,updated_at) VALUES('${siblingCase}','${original}','Closed','Admin','High',now(),now());`);
  equal((await request(reviewerJwt,'/api/admin/cases/'+emailEntry.caseId)).status,403,'reporter association refuses source case evidence');
  const sibling=await request(reviewerJwt,'/api/admin/cases/'+siblingCase);equal(sibling.status,200,'unrelated case remains available');
  equal(sibling.body.sanctions.some(s=>s.caseId===emailEntry.caseId),false,'allowed sibling case cannot project conflicted case sanctions');

  const reader=query(`BEGIN; SELECT pg_advisory_xact_lock_shared(${0x50524553},0); SELECT pg_sleep(3); COMMIT;`);
  for(let n=0;n<100;n++){if(Number(await query(`SELECT count(*) FROM pg_locks WHERE locktype='advisory' AND database=(SELECT oid FROM pg_database WHERE datname='${database}') AND classid=${0x50524553} AND objid=0 AND mode='ShareLock' AND granted`)))break;if(n===99)throw new Error('Fixture preservation reader not established.');await sleep(10);}
  const detailRead=request(adminToken,'/api/admin/cases/'+visibleCase);let detailWait=false;
  for(let n=0;n<100;n++){if(Number(await query(`SELECT count(*) FROM pg_locks WHERE locktype='advisory' AND database=(SELECT oid FROM pg_database WHERE datname='${database}') AND classid=${0x50524553} AND objid=0 AND mode='ExclusiveLock' AND NOT granted`))){detailWait=true;break;}await sleep(10);}
  equal(detailWait,true,'case detail obtains exclusive membership authority before evidence projection');await reader;
  equal((await detailRead).status,200,'allowed detail resumes after authority is released');
  equal(await query(`SELECT count(*) FROM sensitive_access_log WHERE actor_account_id='${otherId}' AND resource='moderation-case' AND resource_id='${emailEntry.caseId}' AND status=403`),'1','refused case evidence read is audited without content');

  equal((await request(otherJwt,'/api/admin/banned-identifiers/release','POST',{identifierIds:[emailEntry.id],reason:'Fictional conflicted release request'})).status,403,'personal conflict refuses release');

  // Actual local Keycloak deletion of this fictional person; registry is independently retained.
  const freshOriginal=await memberToken(browser,member);
  equal((await request(freshOriginal,'/api/me/deletion','POST',{confirm:true,immediately:true,deleteFounderToken:false})).status,200,'terminated member can request deletion');
  await run('dotnet',[dll,'accounts','purge',original],{cwd:apiDir,env});
  equal(await query(`SELECT status FROM accounts WHERE id='${original}'`),'Deleted','fictional original deleted');
  equal((await request(otherJwt,'/api/admin/Cases/'+emailEntry.caseId)).status,403,'retained personal association still refuses deleted-source evidence read');
  equal(await query(`SELECT count(*) FROM banned_identifiers WHERE status='Active'`),'2','deletion retains active restrictions');
  equal(await query(`SELECT count(*) FROM terminated_email_holds`),'0','new registry flow does not recreate legacy email marker');
  const {localRealmApi}=await import('../../scripts/auth/provision.mjs');const realm=await localRealmApi({realm:'swoovr-members-dev'});
  const returning={email:member.email,password:'Fictional-return-'+crypto.randomBytes(6).toString('hex')};
  await realm('/users','POST',{username:returning.email,email:returning.email,emailVerified:true,enabled:true,firstName:'Fictional Reassigned',credentials:[{type:'password',value:returning.password,temporary:false}]});
  const returningJwt=await memberToken(browser,returning);
  assert.notEqual(claims(returningJwt).sub,claims(memberJwt).sub);checks++;
  equal((await request(returningJwt,'/api/session')).status,403,'new authentication identity cannot reuse restricted email');
  const release={identifierIds:[emailEntry.id],reason:'Fictional legitimate reassignment independently reviewed with sufficient ownership evidence.'};
  await query(`CREATE FUNCTION fixture_reject_release_note() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF EXISTS(SELECT 1 FROM ban_transitions WHERE id=NEW.transition_id AND action='revoked') THEN RAISE EXCEPTION 'Fictional release note failure'; END IF; RETURN NEW; END $$;
   CREATE TRIGGER fixture_reject_release_note BEFORE INSERT ON ban_case_notes FOR EACH ROW EXECUTE FUNCTION fixture_reject_release_note();`);
  equal((await request(adminToken,'/api/admin/banned-identifiers/release','POST',release)).status>=500,true,'note failure aborts release');
  equal(await query(`SELECT status FROM banned_identifiers WHERE id='${emailEntry.id}'`),'Active','failed release remains blocking');
  equal(await query(`SELECT count(*) FROM ban_transitions WHERE banned_identifier_id='${emailEntry.id}' AND action='revoked'`),'0','failed release leaves no success audit');
  await query(`DROP TRIGGER fixture_reject_release_note ON ban_case_notes; DROP FUNCTION fixture_reject_release_note();`);
  equal((await request(adminToken,'/api/admin/banned-identifiers/release','POST',release)).body.changed,1,'independent identifier release');
  equal((await request(adminToken,'/api/admin/banned-identifiers/release','POST',release)).body.changed,0,'repeat release is idempotent');
  equal(await query(`SELECT count(*) FROM sanctions WHERE account_id='${original}' AND kind='Termination' AND lifted_at IS NULL`),'1','identifier release never reinstates original account');
  equal(await query(`SELECT status FROM banned_identifiers WHERE id='${mobileEntry.id}'`),'Active','other identifier not released');
  equal((await request(returningJwt,'/api/session')).status,200,'released email can register legitimately');
  equal((await request(returningJwt,'/api/me/phone','POST',{number:'+61491570159'})).status,403,'unreviewed mobile still blocked');
  const deniedPreview=await request(adminToken,`/api/admin/accounts/${original}/reinstatement`);equal(deniedPreview.status,403,'preview requires separate reinstatement capability');equal(deniedPreview.body.code,'elevated-access-required','preview needs separate reinstatement capability');
  await grant(adminToken,'account-reinstatement');
  const deletedPreview=await request(adminToken,`/api/admin/accounts/${original}/reinstatement`);equal(deletedPreview.status,200,'authorized deleted-account preview');equal(deletedPreview.body.eligible,false,'preview cannot offer deleted-account restoration');
  equal((await request(adminToken,`/api/admin/accounts/${original}/reinstate`,'POST',{revokeIdentifierIds:[mobileEntry.id],reason:'Fictional review'})).status,409,'deleted account cannot be resurrected');

  const second=await createMember('workflow-reinstate'),secondJwt=await memberToken(browser,second);
  await request(secondJwt,'/api/session');const secondId=await query(`SELECT id FROM accounts WHERE subject='${claims(secondJwt).sub}'`);
  equal((await request(adminToken,`/api/admin/staff-links/${otherId}`,'PUT',{memberAccountId:secondId})).status,204,'replace personal link after prior deletion');
  equal((await request(otherJwt,'/api/admin/cases/'+emailEntry.caseId)).status,403,'relinking cannot restore access to retained original case');
  equal((await terminate(secondId)).status,200,'second original terminated');
  const secondBan=await query(`SELECT b.id FROM banned_identifiers b JOIN moderation_cases c ON b.case_id=c.id WHERE c.subject_account_id='${secondId}'`);
  const terminationId=await query(`SELECT id FROM sanctions WHERE account_id='${secondId}' AND kind='Termination'`);
  equal((await request(adminToken,`/api/admin/sanctions/${terminationId}/lift`,'POST',{reason:'Fictional bypass attempt'})).status,409,'ordinary lift cannot bypass reinstatement');
  const appealId=crypto.randomUUID();
  await query(`INSERT INTO appeals(id,sanction_id,account_id,status,created_at) VALUES('${appealId}','${terminationId}','${secondId}','Pending',now())`);
  equal((await request(adminToken,`/api/admin/appeals/${appealId}/decide`,'POST',{outcome:'overturn'})).status,409,'appeal overturn cannot bypass reinstatement');
  equal(await query(`SELECT status FROM appeals WHERE id='${appealId}'`),'Pending','refused overturn leaves appeal unchanged');

  equal((await request(adminToken,`/api/admin/accounts/${secondId}/reinstate`,'POST',{revokeIdentifierIds:[],reason:'Fictional incomplete reconciliation'})).status,409,'reinstatement must explicitly reconcile identifiers');
  equal((await request(adminToken,`/api/admin/accounts/${secondId}/reinstate`,'POST',{revokeIdentifierIds:[secondBan,mobileEntry.id],reason:'Fictional unrelated identifiers'})).status,409,'reinstatement cannot release unrelated restrictions');
  equal((await request(adminToken,`/api/admin/accounts/${secondId}/sanctions`,'POST',{action:'restrict',reason:'Fictional independent restriction',days:7,noMessages:true})).status,200,'independent restriction exists');
  const restorePreview=await request(adminToken,`/api/admin/accounts/${secondId}/reinstatement`);equal(restorePreview.status,200,'fresh authorized reinstatement preview');equal(restorePreview.body.eligible,true,'server prepares eligible current source');assert.deepEqual(restorePreview.body.requiredIdentifierIds,[secondBan]);checks++;equal(restorePreview.body.otherSanctions,true,'preview does not promise other restrictions end');
  equal((await request(adminToken,`/api/admin/accounts/${secondId}/reinstate`,'POST',{revokeIdentifierIds:[secondBan],reason:'Fictional account review completed; applicable restriction explicitly released.'})).status,200,'explicit account reinstatement');
  equal(await query(`SELECT count(*) FROM sanctions WHERE account_id='${secondId}' AND kind='Termination' AND lifted_at IS NULL`),'0','termination lifted');
  equal((await request(secondJwt,'/api/me/profile')).body.code,'account-restricted','other sanctions remain independent');
  equal(await query(`SELECT status FROM banned_identifiers WHERE id='${secondBan}'`),'Revoked','selected identifier reconciled');
  equal(await query(`SELECT status FROM banned_identifiers WHERE id='${mobileEntry.id}'`),'Active','unrelated identifier remains active');
  equal(await query(`SELECT count(*) FROM ban_transitions WHERE banned_identifier_id='${secondBan}' AND action='account-reinstated'`),'1','reinstatement explanation has immutable case history');
  equal(await query(`SELECT count(*) FROM member_notices WHERE account_id='${secondId}' AND kind='SanctionLifted'`),'1','member sees the reinstatement outcome notice');

  const reviewed=await request(adminToken,`/api/admin/banned-identifiers/${mobileEntry.id}/review`,'POST',{reason:'Fictional necessity review: permanent mobile restriction remains justified on reviewed evidence.'});
  equal(reviewed.status,200,'authorized necessity review');equal(reviewed.body.status,'active','necessity review does not revoke or renew expiry');
  equal(await query(`SELECT count(*) FROM ban_transitions WHERE banned_identifier_id='${mobileEntry.id}' AND action='necessity-reviewed'`),'1','necessity review is immutable case history');
  equal((await request(adminToken,`/api/admin/banned-identifiers/${emailEntry.id}/review`,'POST',{reason:'Fictional invalid review attempt'})).status,409,'review cannot reactivate a revoked identifier');
  const due=await request(adminToken,'/api/admin/banned-identifiers?due=true');equal(due.status,200,'restricted review queue');
  equal(due.body.some(b=>b.id===mobileEntry.id),false,'freshly reviewed restriction is not overdue');

  const early=await createMember('workflow-release-first'),earlyJwt=await memberToken(browser,early);await request(earlyJwt,'/api/session');
  const earlyId=await query(`SELECT id FROM accounts WHERE subject='${claims(earlyJwt).sub}'`);await terminate(earlyId);
  const earlyBan=await query(`SELECT b.id FROM banned_identifiers b JOIN moderation_cases c ON b.case_id=c.id WHERE c.subject_account_id='${earlyId}'`);
  equal((await request(adminToken,'/api/admin/banned-identifiers/release','POST',{identifierIds:[earlyBan],reason:'Fictional correction reviewed before original account deletion.'})).status,200,'identifier can be released before original deletion');
  const earlyFresh=await memberToken(browser,early);await request(earlyFresh,'/api/me/deletion','POST',{confirm:true,immediately:true,deleteFounderToken:false});
  await run('dotnet',[dll,'accounts','purge',earlyId],{cwd:apiDir,env});
  equal(await query(`SELECT status FROM banned_identifiers WHERE id='${earlyBan}'`),'Revoked','later deletion cannot reactivate a reviewed identifier');
  equal(await query(`SELECT count(*) FROM terminated_email_holds`),'0','later deletion cannot reintroduce a legacy blocking marker');
  const before=await query(`SELECT count(*) FROM banned_identifiers`);await run('dotnet',[dll,'bans','backfill'],{cwd:apiDir,env});
  equal(await query(`SELECT count(*) FROM banned_identifiers`),before,'known-provenance backfill is idempotent');
  equal(await query(`SELECT status FROM banned_identifiers WHERE id='${earlyBan}'`),'Revoked','backfill cannot silently reactivate a released restriction');

  // Consistent old temporal fixture: real current local login, synthetic historical enforcement clocks/events.
  // Do not backdate immutable events from the live workflow above or claim historical provider activity.
  const historical=await createMember('workflow-disposition'),historicalJwt=await memberToken(browser,historical);await request(historicalJwt,'/api/session');
  const historicalId=await query(`SELECT id FROM accounts WHERE subject='${claims(historicalJwt).sub}'`);
  const {dispositionCase,dispositionBan,dispositionTerm,fingerprint}=await require('./historical-fixture.cjs').releasedHistory(query,{accountId:historicalId,staffId:adminId,email:historical.email,key});
  const oldClosed=await query(`SELECT closed_at FROM moderation_cases WHERE id='${dispositionCase}'`);
  let refused=false;try{await run('dotnet',[dll,'retention','purge'],{cwd:apiDir,env:{...env,Evidence__HoldCheckUnavailable:'true'}});}catch{refused=true;}
  equal(refused,true,'unavailable preservation refuses coordinated disposal');equal(await query(`SELECT count(*) FROM banned_identifiers WHERE id='${dispositionBan}'`),'1','failed-closed coordinator keeps due source');
  await Promise.all([run('dotnet',[dll,'retention','purge'],{cwd:apiDir,env}),run('dotnet',[dll,'retention','purge'],{cwd:apiDir,env})]);
  equal(await query(`SELECT count(*) FROM banned_identifiers WHERE id='${dispositionBan}'`),'0','coordinated workers dispose due fingerprint once');
  equal(await query(`SELECT count(*) FROM ban_transitions WHERE banned_identifier_id='${dispositionBan}'`),'0','old match snapshots and reasons disposed');
  equal(await query(`SELECT count(*) FROM ban_case_notes WHERE case_id='${dispositionCase}'`),'0','old linked note text disposed');
  equal(await query(`SELECT count(*) FROM ban_dispositions WHERE id='${dispositionBan}'`),'1','minimal nonmatchable source receipt remains');
  await run('dotnet',[dll,'bans','backfill'],{cwd:apiDir,env});
  equal(await query(`SELECT count(*) FROM banned_identifiers WHERE case_id='${dispositionCase}'`),'0','backfill cannot reconstruct a disposed released restriction');
  const minimizedPreview=await request(adminToken,`/api/admin/accounts/${historicalId}/reinstatement`);
  equal(minimizedPreview.status,200,'authorized minimized source preview');equal(minimizedPreview.body.eligible,true,'lawful history expiry does not permanently block original account review');
  assert.deepEqual(minimizedPreview.body.requiredIdentifierIds,[]);checks++;equal(minimizedPreview.body.dispositions[0].id,dispositionBan,'source disposition shown independently of matching');
  equal(JSON.stringify(minimizedPreview.body).includes(fingerprint.toString('base64')),false,'minimized preview has no fingerprint snapshot');
  equal((await request(adminToken,`/api/admin/accounts/${historicalId}/reinstate`,'POST',{revokeIdentifierIds:[],reason:'Fictional new reviewed reinstatement after lawful match history disposal.'})).status,200,'original account can be reviewed and reinstated after match history expiry');
  equal(await query(`SELECT count(*) FROM sanctions WHERE id='${dispositionTerm}' AND lifted_at IS NULL`),'0','actual restricted workflow lifts original termination');
  equal(await query(`SELECT count(*) FROM ban_disposition_reviews WHERE disposition_id='${dispositionBan}' AND case_id='${dispositionCase}'`),'1','new reinstatement has its own immutable case review');
  equal(await query(`SELECT closed_at FROM moderation_cases WHERE id='${dispositionCase}'`),oldClosed,'new review does not rewrite original closure');
  await run('dotnet',[dll,'retention','purge'],{cwd:apiDir,env});
  equal(await query(`SELECT count(*) FROM ban_dispositions WHERE id='${dispositionBan}'`),'1','new review independently prevents immediate receipt expiry');
  const restoredPreview=await request(adminToken,`/api/admin/accounts/${historicalId}/reinstatement`);equal(restoredPreview.body.blockedCode,'no-termination','post-action preview remains a snapshot rather than enabling another restore');
  equal(restoredPreview.body.dispositions[0].reviews.length,1,'authorized staff can read newly recorded case review');

  const receiptHold=crypto.randomUUID();
  await query(`INSERT INTO legal_holds(id,status,reason,source,placed_by_role,placed_at,review_due,case_id,related_account_ids,retention_case_ids)
    VALUES('${receiptHold}','Active','Fictional post-disposal preservation scope','staff','Legal',now(),now()+interval '1 month','${dispositionCase}',ARRAY['${historicalId}']::uuid[],ARRAY['${dispositionCase}']::uuid[]);
   INSERT INTO legal_hold_scopes(hold_id,kind,target_id) VALUES('${receiptHold}','Account','${historicalId}');
   INSERT INTO ban_check_attempts(caller_hash,key_version,identifier_type,account_id,matched_ban_ids,at)
    VALUES(decode(repeat('61',32),'hex'),1,'Email',NULL,ARRAY['${dispositionBan}']::uuid[],now()-interval '2 days');`);
  await run('dotnet',[dll,'retention','purge'],{cwd:apiDir,env});
  equal(await query(`SELECT count(*) FROM ban_check_attempts WHERE '${dispositionBan}'::uuid=ANY(matched_ban_ids)`),'1','new hold protects historical match references through minimal disposition');
  await query(`UPDATE legal_holds SET status='Released' WHERE id='${receiptHold}'`);await run('dotnet',[dll,'retention','purge'],{cwd:apiDir,env});
  equal(await query(`SELECT count(*) FROM ban_check_attempts WHERE '${dispositionBan}'::uuid=ANY(matched_ban_ids)`),'0','release resumes the independent quota TTL for disposed-source references');

  const mixed=await request(adminToken,`/API/ADMIN/BANNED-IDENTIFIERS/${mobileEntry.id}`);equal(mixed.status,200,'matched route spelling remains authorized');
  equal((await request(null,'/api/admin/banned-identifiers/lookup','POST',{type:'email',value:member.email,purpose:'Fictional unauthenticated lookup'})).status,401,'unauthenticated registry lookup refused');
  equal(Number(await query(`SELECT count(*) FROM sensitive_access_log WHERE resource='banned-identifiers' AND status=401`))>0,true,'authorization refusal is audited');
  equal(Number(await query(`SELECT count(*) FROM sensitive_access_log WHERE resource='banned-identifiers' AND status=500`))>0,true,'failed required-note operation is audited with its failure status');
  equal(Number(await query(`SELECT count(*) FROM sensitive_access_log WHERE resource='banned-identifiers' AND resource_id='${mobileEntry.id}'`))>=2,true,'matched route metadata records the actual opaque ID');
  equal(await query(`SELECT count(*) FROM sensitive_access_log WHERE coalesce(resource_id,'') LIKE '%@%' OR coalesce(resource_id,'') LIKE '%491570%'`),'0','query values and contacts never enter the access log');
  equal(await query(`SELECT count(*) FROM sensitive_access_log WHERE resource='banned-identifiers' AND actor_account_id='${adminId}'`)>0,true,'restricted registry operations logged');
  // D249: active verified-mobile retirement is separate from hash release and account reinstatement.
  const mobileOwner=await createMember('workflow-mobile-retirement'),mobileOwnerJwt=await memberToken(browser,mobileOwner);
  await request(mobileOwnerJwt,'/api/session');const mobileOwnerId=await query(`SELECT id FROM accounts WHERE subject='${claims(mobileOwnerJwt).sub}'`);
  const retiredNumber='+61491570991';
  const pendingOldCode=crypto.randomUUID(),unrelatedOldCode=crypto.randomUUID();
  await query(`INSERT INTO member_phones(account_id,number,verified_at) VALUES('${mobileOwnerId}','${retiredNumber}',now());`);
  await query(`INSERT INTO phone_codes(id,account_id,number,code_hash,ip_hash,created_at,expires_at,failed_attempts)
   VALUES('${pendingOldCode}','${mobileOwnerId}','${retiredNumber}',decode(repeat('21',32),'hex'),decode(repeat('22',32),'hex'),now(),now()+interval '10 minutes',0),
   ('${unrelatedOldCode}','${mobileOwnerId}','+61491570992',decode(repeat('23',32),'hex'),decode(repeat('24',32),'hex'),now(),now()+interval '10 minutes',0);`);
  equal((await terminate(mobileOwnerId)).status,200,'retirement source has real termination provenance');
  const retiringBan=await query(`SELECT b.id FROM banned_identifiers b JOIN moderation_cases c ON c.id=b.case_id WHERE c.subject_account_id='${mobileOwnerId}' AND b.identifier_type='Mobile'`);
  const retireRoute=`/api/admin/banned-identifiers/${retiringBan}/retire-mobile`,retireBody={circumstancesReviewed:true,reason:'Fictional recycled number ownership and case reviewed; preserve original ban.'};
  equal((await request(mobileOwnerJwt,retireRoute,'POST',retireBody)).status,403,'member cannot retire own binding');
  equal((await request(adminToken,retireRoute,'POST',retireBody)).status,409,'active restriction cannot be retired');
  await request(adminToken,'/api/admin/banned-identifiers/release','POST',{identifierIds:[retiringBan],reason:'Fictional independent recycled number review.'});
  equal((await request(adminToken,retireRoute,'POST',{...retireBody,circumstancesReviewed:false})).status,400,'binding retirement requires separate circumstances review');
  await query(`UPDATE member_phones SET number='+61491570992' WHERE account_id='${mobileOwnerId}'`);
  equal((await request(adminToken,retireRoute,'POST',retireBody)).status,409,'a different current binding is never removed');
  await query(`UPDATE member_phones SET number='${retiredNumber}' WHERE account_id='${mobileOwnerId}'`);
  await request(adminToken,`/api/admin/staff-links/${otherId}`,'PUT',{memberAccountId:mobileOwnerId});
  equal((await request(otherJwt,retireRoute,'POST',retireBody)).status,403,'personal association refuses binding retirement');
  const originalSecret=env.MemberAdmin__ClientSecret;
  await stop();env.MemberAdmin__ClientSecret='fictional-invalid-service-credential';await start('mobile-provider-unavailable');
  equal((await request(adminToken,retireRoute,'POST',retireBody)).status,503,'uncertain provider logout retains binding');
  equal(await query(`SELECT count(*) FROM member_phones WHERE account_id='${mobileOwnerId}'`),'1','provider refusal leaves unique binding intact');
  equal(await query(`SELECT count(*) FROM ban_transitions WHERE banned_identifier_id='${retiringBan}' AND action='mobile-binding-retired'`),'0','provider refusal cannot record success');
  await stop();env.MemberAdmin__ClientSecret=originalSecret;await start('mobile-provider-restored');
  await query(`CREATE FUNCTION fixture_reject_retirement_note() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF EXISTS(SELECT 1 FROM ban_transitions WHERE id=NEW.transition_id AND action='mobile-binding-retired') THEN RAISE EXCEPTION 'Fictional retirement note failure'; END IF; RETURN NEW; END $$;
    CREATE TRIGGER fixture_reject_retirement_note BEFORE INSERT ON ban_case_notes FOR EACH ROW EXECUTE FUNCTION fixture_reject_retirement_note();`);
  equal((await request(adminToken,retireRoute,'POST',retireBody)).status>=500,true,'required retirement note failure aborts local operation');
  equal((await fs.readFile(path.join(folder,'mobile-provider-restored.log'),'utf8')).includes('Fictional retirement note failure'),true,'injected note trigger was actually reached, not masked by another constraint');
  equal(await query(`SELECT count(*) FROM member_phones WHERE account_id='${mobileOwnerId}'`),'1','database rollback preserves binding after safe remote logout');
  equal(await query(`SELECT count(*) FROM contact_histories WHERE account_id='${mobileOwnerId}'`),'0','failed retirement leaves no partial archive');
  equal(await query(`SELECT count(*) FROM accounts WHERE id='${mobileOwnerId}' AND sessions_revoked_at IS NULL`),'1','failed note rolls back API revocation watermark with the binding transaction');
  equal(await query(`SELECT count(*) FROM phone_codes WHERE id='${pendingOldCode}' AND used_at IS NULL`),'1','note rollback also rolls back pending-code invalidation');
  await query(`DROP TRIGGER fixture_reject_retirement_note ON ban_case_notes; DROP FUNCTION fixture_reject_retirement_note();`);
  // A new fictional session proves the successful retry actually signs out current sessions.
  await memberToken(browser,mobileOwner);
  equal((await realm('/users/'+claims(mobileOwnerJwt).sub+'/sessions')).length>0,true,'source has actual Keycloak session before retirement');
  const retired=await request(adminToken,retireRoute,'POST',retireBody);
  equal(retired.status,200,'retirement success status; code='+(retired.body?.code||'none'));
  equal(retired.body.changed,1,'separate retirement frees current reviewed binding');
  equal((await realm('/users/'+claims(mobileOwnerJwt).sub+'/sessions')).length,0,'retirement revokes actual source Keycloak sessions');
  equal(await query(`SELECT count(*) FROM member_phones WHERE account_id='${mobileOwnerId}'`),'0','only the original verified-phone binding removed');
  equal(await query(`SELECT email FROM accounts WHERE id='${mobileOwnerId}'`),mobileOwner.email,'unrelated original email preserved');
  equal(await query(`SELECT count(*) FROM phone_codes WHERE id='${pendingOldCode}' AND used_at IS NOT NULL`),'1','same-contact pending challenge cannot reclaim a retired number');
  equal(await query(`SELECT count(*) FROM phone_codes WHERE id='${unrelatedOldCode}' AND used_at IS NULL`),'1','unrelated pending challenge is preserved');
  equal(await query(`SELECT count(*) FROM sanctions WHERE account_id='${mobileOwnerId}' AND kind='Termination' AND lifted_at IS NULL`),'1','retirement does not restore original account');
  equal(await query(`SELECT count(*) FROM contact_histories WHERE account_id='${mobileOwnerId}' AND change_reason='staff-binding-retirement'`),'1','restricted encrypted prior binding archived');
  equal(await query(`SELECT count(*) FROM ban_case_notes n JOIN ban_transitions t ON n.transition_id=t.id WHERE t.banned_identifier_id='${retiringBan}' AND t.action='mobile-binding-retired'`),'1','immutable success and associated case note committed once');
  equal((await request(mobileOwnerJwt,'/api/session')).status,401,'pre-retirement JWT is refused by the API watermark');
  await sleep(31500); // Conservative30s issuer skew plus whole-second auth_time resolution.
  const retainedCredentials=await memberToken(browser,mobileOwner);
  equal((await request(retainedCredentials,'/api/me/profile')).body.code,'account-suspended','unrelated provider password/email still authenticate but original account ban remains');
  equal((await request(adminToken,retireRoute,'POST',retireBody)).body.changed,0,'same completed retirement retry is idempotent');
  await query(`INSERT INTO member_phones(account_id,number,verified_at) VALUES('${mobileOwnerId}','+61491570992',now());`);
  equal((await request(adminToken,retireRoute,'POST',retireBody)).status,409,'old completion cannot remove a later binding');
  await query(`DELETE FROM member_phones WHERE account_id='${mobileOwnerId}'`);
  const mobileRecipient=await createMember('workflow-mobile-recipient'),recipientJwt=await memberToken(browser,mobileRecipient);
  await request(recipientJwt,'/api/session');
  equal((await request(recipientJwt,'/api/me/phone','POST',{number:retiredNumber})).status,200,'released retired number can begin ordinary verification');

  const recipientId=await query(`SELECT id FROM accounts WHERE subject='${claims(recipientJwt).sub}'`),fictionalCode='123456';
  const verifiedHash=crypto.createHmac('sha256',Buffer.from(env.Phone__CodeKey)).update(recipientId.replaceAll('-','')+'|'+retiredNumber+'|'+fictionalCode).digest('hex');
  await query(`UPDATE phone_codes SET code_hash=decode('${verifiedHash}','hex') WHERE account_id='${recipientId}' AND number='${retiredNumber}'`);
  equal((await request(recipientJwt,'/api/me/phone/confirm','POST',{number:retiredNumber,code:fictionalCode})).status,200,'ordinary confirm can attach released number after active binding retirement');
  equal(await query(`SELECT number FROM member_phones WHERE account_id='${recipientId}'`),retiredNumber,'unique binding belongs only to newly verified fictional recipient');
  // The code above is a synthetic test fixture, not SMS delivery/current ownership evidence.
  const recoveryPreview=await request(adminToken,`/api/admin/accounts/${mobileOwnerId}/reinstatement`);
  equal(recoveryPreview.body.eligible,true,'old-account restoration still requires its independent reviewed workflow');
  equal((await request(adminToken,`/api/admin/accounts/${mobileOwnerId}/reinstate`,'POST',{revokeIdentifierIds:recoveryPreview.body.requiredIdentifierIds,reason:'Fictional independent original-account recovery reviewed after mobile retirement.'})).status,200,'only explicit separate account recovery reinstates original member');
  equal((await request(mobileOwnerJwt,'/api/session')).status,401,'old JWT stays refused after later reinstatement');
  equal((await request(retainedCredentials,'/api/session')).status,200,'a fresh authentication with preserved unrelated credentials works after reviewed recovery');

  console.log(`PASS: registry population/release/reinstatement workflows (${checks} checks; fictional local identities, real isolated API/PostgreSQL; no delivery or hosted Entra evidence).`);
 } finally {
  if(replica&&replica.exitCode===null){const exited=once(replica,'exit');replica.kill('SIGTERM');await exited;}
  await stop();
  const privateLogs=path.join('.local/log-backups','mobile-retirement-workflow-'+new Date().toISOString().replaceAll(/[^0-9TZ]/g,''));
  await fs.mkdir(privateLogs,{recursive:true,mode:0o700});
  for(const file of await fs.readdir(folder))if(file.endsWith('.log')){const target=path.join(privateLogs,file);await fs.copyFile(path.join(folder,file),target);await fs.chmod(target,0o600);}
  console.log('PRIVATE LOGS: '+privateLogs);
  await edge.close();if(created)await sql(`DROP DATABASE ${database} WITH (FORCE)`);await fs.rm(folder,{recursive:true,force:true});
 }
})().catch(error=>{console.error(error);process.exitCode=1;});
