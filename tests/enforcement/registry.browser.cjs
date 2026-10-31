// Fictional local Keycloak tokens + an actual isolated API/throwaway PostgreSQL. No owner application DB,
// real registration/ownership verification, SMS/push/email delivery or external screening is claimed.
const assert=require('node:assert/strict'),crypto=require('node:crypto');
const {execFile,spawn}=require('node:child_process'),{once}=require('node:events');
const net=require('node:net'),fs=require('node:fs/promises'),path=require('node:path'),os=require('node:os');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
const {sql,localEnv,createMember,memberToken,claims}=require('../auth/local-tokens.cjs');
const run=(command,args,options={})=>new Promise((resolve,reject)=>execFile(command,args,options,(error,out,err)=>error?reject(new Error(command+' failed: '+err.slice(-500))):resolve(out.trim())));
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
(async()=>{
 const database='swoovr_bans_'+crypto.randomUUID().replaceAll('-',''),folder=await fs.mkdtemp(path.join(os.tmpdir(),'swoovr-bans-'));
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
   Founders__Secrets__1:await localEnv('SWOOVR_FOUNDER_SECRET_V1'),Bans__HmacKeys__1:key.toString('base64'),
   Beta__Enabled:'false',Phone__Required:'false',Phone__CodeKey:'fictional-isolated-phone-code-key-123456',Accounts__HoldKey:'fictional-isolated-legacy-hold-key-123456',Media__Store:'local',Media__Local__Path:folder,Media__Screening__Worker:'false',Retention__Worker:'false'};
  await run('dotnet',[dll,'migrate'],{cwd:apiDir,env});
  const start=async label=>{
   const handle=await fs.open(path.join(folder,label+'.log'),'w');host=spawn('dotnet',[dll],{cwd:apiDir,env,stdio:['ignore',handle.fd,handle.fd]});await handle.close();
   for(let n=0;n<100;n++) {if(host.exitCode!==null)throw new Error('Isolated registry host exited.');try{if((await fetch(base+'/health/live')).ok)return;}catch{}await sleep(100);}throw new Error('Isolated host startup timeout.');
  };
  await start('first');
  const blocked=await createMember('registry-blocked'),token=await memberToken(browser,blocked);
  const source=crypto.randomUUID(),review=crypto.randomUUID(),ban=crypto.randomUUID();
  const hash=crypto.createHmac('sha256',key).update('swoovr/banned-identifier/email\0'+blocked.email.trim().toLowerCase()).digest('hex');
  await query(`INSERT INTO accounts(id,issuer,subject,realm,email,status,created_at,last_seen_at,is_root_admin) VALUES('${source}','https://issuer.example.test','fictional-source-${source}','Members',NULL,'Deleted',now(),now(),false);
   INSERT INTO moderation_cases(id,subject_account_id,status,queue,priority,created_at,updated_at,closed_at) VALUES('${review}','${source}','Closed','Admin','Normal',now(),now(),now());
   INSERT INTO banned_identifiers(id,identifier_type,identifier_hash,key_version,case_id,ban_reason_code,status,banned_at,match_count) VALUES('${ban}','Email',decode('${hash}','hex'),1,'${review}','permanent-account-ban','Active',now(),0);`);
  const request=(token,route,method='GET',body,target=base)=>fetch(target+route,{method,headers:{Authorization:'Bearer '+token,'Swoovr-Version':'2026-10-28',...(body?{'Content-Type':'application/json'}:{})},...(body?{body:JSON.stringify(body)}:{})});
  const response=await request(token,'/api/session');
  equal(response.status,403,'active banned email blocks first application registration');
  equal(response.headers.get('cache-control'),'no-store','identifier refusal is not cacheable');
  const problem=await response.json();equal(problem.code,'identifier-restricted','neutral member error code');
  equal(problem.title,'This email address cannot currently be used with Swoovr. If you believe this is incorrect, please contact Swoovr Support for assistance.','approved message');
  equal(await query(`SELECT count(*) FROM accounts WHERE subject='${claims(token).sub}'`),'0','no application account or profile created');
  equal(await query(`SELECT match_count FROM banned_identifiers WHERE id='${ban}'`),'1','attempt recorded without plaintext identifier');
  assert.ok(!JSON.stringify(problem).includes(review)&&!JSON.stringify(problem).includes(hash)&&!JSON.stringify(problem).includes(source));checks++;
  const failed=await createMember('registry-profile-failure'),failedToken=await memberToken(browser,failed);
  await query(`CREATE FUNCTION fixture_reject_profile() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
   IF EXISTS(SELECT 1 FROM accounts WHERE id=NEW.account_id AND email='${failed.email}') THEN RAISE EXCEPTION 'Fictional profile insert failure'; END IF; RETURN NEW; END $$;
   CREATE TRIGGER fixture_reject_profile BEFORE INSERT ON member_profiles FOR EACH ROW EXECUTE FUNCTION fixture_reject_profile();`);
  const failedRegistration=await request(failedToken,'/api/session');
  equal(failedRegistration.status>=500,true,'synthetic profile failure is not successful registration');
  equal(await query(`SELECT count(*) FROM accounts WHERE subject='${claims(failedToken).sub}'`),'0','failed first profile cannot leave a partial application registration');
  await query(`DROP TRIGGER fixture_reject_profile ON member_profiles; DROP FUNCTION fixture_reject_profile();`);
  equal((await request(failedToken,'/api/session')).status,200,'registration retries after the synthetic database failure clears');
  equal(await query(`SELECT count(*) FROM member_profiles p JOIN accounts a ON p.account_id=a.id WHERE a.subject='${claims(failedToken).sub}'`),'1','retry creates one complete registration');

  const flagged=await createMember('registry-case-failure'),flaggedToken=await memberToken(browser,flagged);
  const legacyHash=crypto.createHmac('sha256',env.Accounts__HoldKey).update(flagged.email.trim().toLowerCase()).digest('hex');
  await query(`INSERT INTO terminated_email_holds(email_hash,created_at,expires_at) VALUES(decode('${legacyHash}','hex'),now(),now()+interval '1 day');
   CREATE FUNCTION fixture_reject_case() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
    IF NEW.system_flag='returning-terminated' THEN RAISE EXCEPTION 'Fictional case insert failure'; END IF; RETURN NEW; END $$;
   CREATE TRIGGER fixture_reject_case BEFORE INSERT ON moderation_cases FOR EACH ROW EXECUTE FUNCTION fixture_reject_case();`);
  const failedCase=await request(flaggedToken,'/api/session');
  equal(failedCase.status>=500,true,'failure after profile insert cannot silently omit the required legacy review case');
  equal(await query(`SELECT count(*) FROM accounts WHERE subject='${claims(flaggedToken).sub}'`),'0','case failure rolls back the whole new registration');
  await query(`DROP TRIGGER fixture_reject_case ON moderation_cases; DROP FUNCTION fixture_reject_case();`);
  equal((await request(flaggedToken,'/api/session')).status,200,'registration retries the required legacy case after fault clears');
  equal(await query(`SELECT count(*) FROM moderation_cases c JOIN accounts a ON c.subject_account_id=a.id WHERE a.subject='${claims(flaggedToken).sub}' AND c.system_flag='returning-terminated'`),'1','retry commits the required legacy review exactly once');

  const fingerprint=(type,value,k=key)=>crypto.createHmac('sha256',k).update('swoovr/banned-identifier/'+type+'\0'+value.trim().toLowerCase()).digest('hex');
  const addBan=async(type,value,version=1,k=key)=>{
   const id=crypto.randomUUID();await query(`INSERT INTO banned_identifiers(id,identifier_type,identifier_hash,key_version,case_id,ban_reason_code,status,banned_at,match_count) VALUES('${id}','${type}',decode('${fingerprint(type.toLowerCase(),value,k)}','hex'),${version},'${review}','permanent-account-ban','Active',now(),0);`);return id;
  };
  const allowed=await createMember('registry-allowed'),allowedToken=await memberToken(browser,allowed);
  equal((await request(allowedToken,'/api/session')).status,200,'unrestricted application registration works');
  const account=await query(`SELECT id FROM accounts WHERE subject='${claims(allowedToken).sub}'`);
  await query(`INSERT INTO member_phones(account_id,number,verified_at) VALUES('${account}','+61491570158',now());`);
  const mobile=await addBan('Mobile','+61491570159');
  for(const number of ['0491 570 159','+61491570159']){
   const refused=await request(allowedToken,'/api/me/phone','POST',{number});equal(refused.status,403,'normalized restricted mobile cannot be attached');const mobileProblem=await refused.json();equal(mobileProblem.code,'identifier-restricted','neutral mobile code');equal(mobileProblem.title,'This mobile number cannot currently be used with Swoovr. If you believe this is incorrect, please contact Swoovr Support for assistance.','approved mobile message');
  }
  equal(await query(`SELECT number FROM member_phones WHERE account_id='${account}'`),'+61491570158','existing legitimate mobile remains unchanged');
  equal(await query(`SELECT count(*) FROM phone_codes WHERE account_id='${account}'`),'0','refused start creates no SMS challenge');
  equal(await query(`SELECT status FROM accounts WHERE id='${account}'`),'Active','identifier refusal never bans the legitimate account');
  const pending='+61491570157',code='123456',phoneKey=env.Phone__CodeKey;
  const codeHash=crypto.createHmac('sha256',phoneKey).update(account.replaceAll('-','')+'|'+pending+'|'+code).digest('hex');
  await query(`INSERT INTO phone_codes(id,account_id,number,code_hash,ip_hash,created_at,expires_at,failed_attempts) VALUES('${crypto.randomUUID()}','${account}','${pending}',decode('${codeHash}','hex'),decode(repeat('00',32),'hex'),now(),now()+interval '5 minutes',0);`);
  const pendingBan=await addBan('Mobile',pending);
  const confirmation=await request(allowedToken,'/api/me/phone/confirm','POST',{number:pending,code});
  equal(confirmation.status,403,'a ban added after a synthetic code was issued blocks confirmation');
  equal(await query(`SELECT number FROM member_phones WHERE account_id='${account}'`),'+61491570158','late restriction cannot replace the original verified number');
  equal((await request(allowedToken,'/api/me/profile')).status,200,'legitimate member remains able to use the app');

  // Keycloak update is real local administration of this fixture, with simulated email ownership only.
  const changed='fictional.changed.'+crypto.randomUUID()+'@example.test';await addBan('Email',changed);
  const {localRealmApi}=await import('../../scripts/auth/provision.mjs');const realm=await localRealmApi({realm:'swoovr-members-dev'});
  await realm('/users/'+claims(allowedToken).sub,'PUT',{username:changed,email:changed,emailVerified:true});
  const changedToken=await memberToken(browser,{...allowed,email:changed});
  const changedSession=await request(changedToken,'/api/session');
  equal(changedSession.status,200,'otherwise legitimate member can recover using only the new token');
  equal((await changedSession.json()).emailChangeProblem,'identifier-restricted','neutral change warning without adopting email');
  equal(changedSession.headers.get('cache-control'),'no-store','contact warning is not cacheable');
  equal((await request(changedToken,'/api/me/profile')).status,200,'new-token member retains profile access');
  equal((await request(changedToken,'/api/me/standing')).status,200,'new-token member can reach standing and support');
  equal(await query(`SELECT email FROM accounts WHERE id='${account}'`),allowed.email,'application email was not changed');
  equal(await query(`SELECT status FROM accounts WHERE id='${account}'`),'Active','email refusal does not create an account ban');
  equal((await request(allowedToken,'/api/me/profile')).status,200,'the existing legitimate session remains usable');
  let throttled;
  for(let n=0;n<22;n++){const response=await request(changedToken,'/api/session');equal(response.status,200,'contact quota cannot trap an existing member');throttled=await response.json();}
  equal(throttled.emailChangeProblem,'too-many-requests','quota blocks adoption with a neutral warning');
  equal(await query(`SELECT email FROM accounts WHERE id='${account}'`),allowed.email,'quota refusal keeps accepted contact');
  await realm('/users/'+claims(allowedToken).sub,'PUT',{username:allowed.email,email:allowed.email,emailVerified:true});
  const correctedToken=await memberToken(browser,allowed);
  equal((await (await request(correctedToken,'/api/session')).json()).emailChangeProblem,null,'correcting provider email clears warning without an old token');
  // Explicit fixture-clock advance after proving correction needs no additional admission quota.
  await query(`UPDATE ban_check_attempts SET at=at-interval '2 hours' WHERE account_id='${account}' AND identifier_type='Email'`);
  const acceptedNew='fictional.accepted.'+crypto.randomUUID()+'@example.test';
  await realm('/users/'+claims(allowedToken).sub,'PUT',{username:acceptedNew,email:acceptedNew,emailVerified:true});
  const acceptedToken=await memberToken(browser,{...allowed,email:acceptedNew});
  equal((await (await request(acceptedToken,'/api/session')).json()).emailChangeProblem,null,'allowed newer email is adopted normally');
  equal(await query(`SELECT email FROM accounts WHERE id='${account}'`),acceptedNew,'accepted email advances');
  const oldClaim=await request(allowedToken,'/api/session');
  equal(oldClaim.status,200,'older email token does not trap the existing member');
  equal((await oldClaim.json()).emailChangeProblem,'email-session-refresh-required','older claim is refused only for contact adoption');
  equal(await query(`SELECT email FROM accounts WHERE id='${account}'`),acceptedNew,'older token cannot reverse accepted email');
  equal(await query(`SELECT count(*) FROM contact_histories WHERE account_id='${account}' AND contact_type='Email'`),'1','refused claims create no duplicate contact history');


  // Both token versions precede first Swoovr admission: the accepted initial contact also needs a watermark.
  const reverse=await createMember('registry-reverse'),reverseOld=await memberToken(browser,reverse);
  const reverseEmail='fictional.reverse.'+crypto.randomUUID()+'@example.test';
  await realm('/users/'+claims(reverseOld).sub,'PUT',{username:reverseEmail,email:reverseEmail,emailVerified:true});
  const reverseNew=await memberToken(browser,{...reverse,email:reverseEmail});
  equal((await request(reverseNew,'/api/session')).status,200,'newer token admitted first');
  const reverseResult=await request(reverseOld,'/api/session');
  equal(reverseResult.status,200,'older pre-registration token retains only existing access');
  equal((await reverseResult.json()).emailChangeProblem,'email-session-refresh-required','initial accepted email cannot be reversed by an older token');
  equal(await query(`SELECT email FROM accounts WHERE subject='${claims(reverseOld).sub}'`),reverseEmail,'initial accepted email remains authoritative');
  // Legacy profile with no previously accepted email: the first verified adoption must also establish its clock.
  await query(`UPDATE accounts SET email=NULL,email_accepted_at=NULL WHERE subject='${claims(reverseOld).sub}'`);
  equal((await request(reverseNew,'/api/session')).status,200,'first email attaches to a profile with no accepted email');
  equal((await (await request(reverseOld,'/api/session')).json()).emailChangeProblem,'email-session-refresh-required','null-to-email acceptance also prevents rollback');
  equal(await query(`SELECT count(*) FROM accounts WHERE subject='${claims(reverseOld).sub}' AND email_accepted_at IS NOT NULL`),'1','initial adoption clock committed');
  await addBan('Email',reverse.email);
  const reverseId=await query(`SELECT id FROM accounts WHERE subject='${claims(reverseOld).sub}'`);
  await query(`DELETE FROM member_profiles WHERE account_id='${reverseId}'`);
  equal((await request(reverseOld,'/api/session')).status,403,'existing mapping without a profile cannot bypass admission');
  equal(await query(`SELECT count(*) FROM member_profiles WHERE account_id='${reverseId}'`),'0','refused profileless admission stays incomplete');
  equal((await request(reverseNew,'/api/session')).status,200,'permitted email can restore the synthetic missing profile');
  await query(`UPDATE accounts SET email=NULL,email_accepted_at=NULL WHERE id='${reverseId}'`);
  equal((await request(reverseOld,'/api/session')).status,403,'existing profile without an accepted email cannot bypass admission');
  equal(await query(`SELECT count(*) FROM accounts WHERE id='${reverseId}' AND email IS NULL`),'1','refused initial email not adopted');
  equal((await request(reverseNew,'/api/session')).status,200,'permitted first email still completes adoption');
  await query(`UPDATE accounts SET email_accepted_at=NULL WHERE id='${reverseId}'`);
  equal((await request(reverseNew,'/api/session')).status,200,'legacy writer missing clock does not block same-email access');
  const frozenClock=await query(`SELECT email_accepted_at FROM accounts WHERE id='${reverseId}'`);
  assert.ok(frozenClock);checks++;
  equal((await (await request(reverseOld,'/api/session')).json()).emailChangeProblem,'email-session-refresh-required','conservative legacy boundary rejects older differing claim');
  equal(await query(`SELECT email_accepted_at FROM accounts WHERE id='${reverseId}'`),frozenClock,'legacy boundary freezes once rather than moving on every request');




  const quota=await createMember('registry-quota'),quotaToken=await memberToken(browser,quota);
  equal((await request(quotaToken,'/api/session')).status,200,'quota member registered');
  const replicaProbe=net.createServer();await new Promise(resolve=>replicaProbe.listen(0,'127.0.0.1',resolve));
  const replicaBase='http://127.0.0.1:'+replicaProbe.address().port;await new Promise(resolve=>replicaProbe.close(resolve));
  const replicaLog=await fs.open(path.join(folder,'replica.log'),'w');replica=spawn('dotnet',[dll],{cwd:apiDir,env:{...env,ASPNETCORE_URLS:replicaBase},stdio:['ignore',replicaLog.fd,replicaLog.fd]});await replicaLog.close();
  for(let n=0;n<100;n++){try{if((await fetch(replicaBase+'/health/live')).ok)break;}catch{}if(n===99)throw new Error('Replica start timeout.');await sleep(100);}
  const outcomes=await Promise.all(Array.from({length:25},async(_,i)=>{const r=await request(quotaToken,'/api/me/phone','POST',{number:'+61491570159'},i%2?replicaBase:base);return r.status;}));
  const replicaExited=once(replica,'exit');replica.kill('SIGTERM');await replicaExited;replica=null;
  equal(outcomes.filter(s=>s===403).length,20,'concurrent authenticated mobile checks have a persistent per-caller quota');
  equal(outcomes.filter(s=>s===429).length,5,'excess concurrent checks are rate-limited');
  const quotaId=await query(`SELECT id FROM accounts WHERE subject='${claims(quotaToken).sub}'`);
  await query(`UPDATE ban_check_attempts SET at=now()-interval '2 hours' WHERE account_id='${quotaId}' AND identifier_type='Mobile';`);
  equal((await request(quotaToken,'/api/me/phone','POST',{number:'+61491570159'})).status,403,'sliding-window quota resumes after expiry');

  // The writer uses the same database barrier as authorized production transitions. This is fixture SQL,
  // not acceptance evidence for the staff revocation workflow (which is a separate slice).
  const racing=await createMember('registry-race'),racingToken=await memberToken(browser,racing),racingBan=await addBan('Email',racing.email);
  const writer=query(`BEGIN; SELECT pg_advisory_xact_lock(${0x50524553},0); SELECT pg_sleep(5);
   UPDATE banned_identifiers SET status='Revoked',ban_expires_at=now(),revoked_at=now(),revoked_by='${crypto.randomUUID()}',last_reviewed_at=now() WHERE id='${racingBan}'; COMMIT;`);
  for(let n=0;n<100;n++) {if(Number(await query(`SELECT count(*) FROM pg_locks WHERE locktype='advisory' AND database=(SELECT oid FROM pg_database WHERE datname='${database}') AND classid=${0x50524553} AND objid=0 AND mode='ExclusiveLock' AND granted`))>0)break;if(n===99)throw new Error('Fixture writer barrier not acquired.');await sleep(10);}
  const concurrent=Promise.all(Array.from({length:4},()=>request(racingToken,'/api/session')));
  let waiting=false;
  for(let n=0;n<100;n++){if(Number(await query(`SELECT count(*) FROM pg_locks WHERE locktype='advisory' AND database=(SELECT oid FROM pg_database WHERE datname='${database}') AND classid=${0x50524553} AND objid=0 AND NOT granted`))>0){waiting=true;break;}await sleep(10);}
  equal(waiting,true,'registration actually waits on an in-progress restriction transition');
  equal(await query(`SELECT count(*) FROM accounts WHERE subject='${claims(racingToken).sub}'`),'0','no mapping commits while transition authority is held');
  await writer;const registered=await concurrent;
  equal(registered.every(r=>r.status===200),true,'concurrent registrations see committed revoked state');
  equal(await query(`SELECT count(*) FROM accounts WHERE subject='${claims(racingToken).sub}'`),'1','concurrent first registrations create one mapping');
  equal(await query(`SELECT count(*) FROM member_profiles p JOIN accounts a ON p.account_id=a.id WHERE a.subject='${claims(racingToken).sub}'`),'1','concurrent registration creates one profile');

  const addressHash=crypto.createHmac('sha256',key).update('swoovr/banned-check/address\0'+'127.0.0.1').digest('hex');
  await query(`INSERT INTO ban_check_attempts(caller_hash,address_hash,key_version,identifier_type,account_id,matched_ban_ids,at) SELECT decode(repeat('77',32),'hex'),decode('${addressHash}','hex'),1,'Mobile',NULL,ARRAY[]::uuid[],now() FROM generate_series(1,1000);`);
  const addressLimited=await createMember('registry-address-limit'),addressToken=await memberToken(browser,addressLimited);
  equal((await request(addressToken,'/api/session')).status,429,'persistent address quota applies before another account can be created');
  equal(await query(`SELECT count(*) FROM accounts WHERE subject='${claims(addressToken).sub}'`),'0','address limit cannot leave a new account');
  await query(`DELETE FROM ban_check_attempts WHERE caller_hash=decode(repeat('77',32),'hex');`);

  // Stage both keys, then make2 current; old1 matches still block without plaintext rehashing.
  const key2=Buffer.alloc(32,32);await stop();env.Bans__HmacKeys__2=key2.toString('base64');env.Bans__CurrentKeyVersion='2';await start('rotated');
  equal((await request(token,'/api/session')).status,403,'old active fingerprint remains enforceable with a newer current key');
  const quotaCaller=claims(quotaToken).iss+'\0'+claims(quotaToken).sub;
  const callerHash=crypto.createHmac('sha256',key).update('swoovr/banned-check/caller\0'+quotaCaller).digest('hex');
  await query(`INSERT INTO ban_check_attempts(caller_hash,address_hash,key_version,identifier_type,account_id,matched_ban_ids,at) SELECT decode('${callerHash}','hex'),NULL,1,'Mobile','${quotaId}',ARRAY[]::uuid[],now() FROM generate_series(1,18);`);
  equal((await request(quotaToken,'/api/me/phone','POST',{number:'+61491570159'})).status,403,'new current key still counts historical-key quota attempts');
  equal((await request(quotaToken,'/api/me/phone','POST',{number:'+61491570159'})).status,429,'rotation does not reset the caller quota');
  const second=await createMember('registry-second-key'),secondToken=await memberToken(browser,second);await addBan('Email',second.email,2,key2);
  equal((await request(secondToken,'/api/session')).status,403,'current2 fingerprint also blocks');
  const keyFailureEmail='fictional.key-failure.'+crypto.randomUUID()+'@example.test';
  await realm('/users/'+claims(allowedToken).sub,'PUT',{username:keyFailureEmail,email:keyFailureEmail,emailVerified:true});
  const keyFailureToken=await memberToken(browser,{...allowed,email:keyFailureEmail});
  await stop();delete env.Bans__HmacKeys__1;await start('missing-old-key');
  const preserved=await request(keyFailureToken,'/api/session');equal(preserved.status,200,'unavailable matching cannot trap an existing member');
  equal((await preserved.json()).emailChangeProblem,'identifier-check-unavailable','missing-key warning refuses only contact adoption');
  equal(await query(`SELECT email FROM accounts WHERE id='${account}'`),acceptedNew,'unknown key leaves accepted address intact');

  const fresh=await createMember('registry-missing-key'),freshToken=await memberToken(browser,fresh);
  const unavailable=await request(freshToken,'/api/session');equal(unavailable.status,503,'missing active historical key fails closed for all checks of that type');
  equal((await unavailable.json()).code,'identifier-check-unavailable','key availability failure is neutral');
  equal(await query(`SELECT count(*) FROM accounts WHERE subject='${claims(freshToken).sub}'`),'0','unavailable matching cannot create a new member');
  await query(`UPDATE banned_identifiers SET status='Expired',ban_expires_at=now()-interval '1 second' WHERE identifier_type='Email' AND key_version=1;`);
  equal((await request(freshToken,'/api/session')).status,200,'expired restrictions do not require unavailable old matching keys');
  equal((await (await request(keyFailureToken,'/api/session')).json()).emailChangeProblem,null,'adoption resumes when matching can safely complete');
  equal(await query(`SELECT email FROM accounts WHERE id='${account}'`),keyFailureEmail,'safe new email adopted after availability resumes');
  equal((await request(secondToken,'/api/session')).status,403,'unrelated current active restriction remains enforced');

  // Quota records are operational metadata, not the primary ban/evidence store; live holds override their one-day TTL.
  const hold=crypto.randomUUID();
  await query(`UPDATE ban_check_attempts SET at=now()-interval '2 days';
   UPDATE moderation_cases SET created_at=now()-interval '30 months',updated_at=now()-interval '30 months',closed_at=now()-interval '30 months' WHERE id='${review}';
   INSERT INTO legal_holds(id,status,reason,source,placed_by_role,placed_at,review_due,retention_case_ids,related_account_ids)
   VALUES('${hold}','Active','Fictional quota preservation','staff','Legal',now(),now()+interval '14 days',ARRAY['${review}']::uuid[],ARRAY['${source}']::uuid[]);
   INSERT INTO legal_hold_scopes(hold_id,kind,target_id) VALUES('${hold}','Account','${source}');`);
  const beforeUnknown=await query(`SELECT count(*) FROM ban_check_attempts`);
  await query(`UPDATE legal_holds SET related_account_ids=NULL WHERE id='${hold}';`);
  await assert.rejects(run('dotnet',[dll,'retention','purge'],{cwd:apiDir,env}),/preservation status or membership/);checks++;
  equal(await query(`SELECT count(*) FROM ban_check_attempts`),beforeUnknown,'unknown preserved membership stops operational metadata expiry');
  await query(`UPDATE legal_holds SET related_account_ids=ARRAY['${source}']::uuid[],retention_case_ids=NULL WHERE id='${hold}';`);
  await assert.rejects(run('dotnet',[dll,'retention','purge'],{cwd:apiDir,env}),/preservation status or membership/);checks++;
  equal(await query(`SELECT count(*) FROM ban_check_attempts`),beforeUnknown,'unknown case provenance cannot be treated as empty');
  await query(`UPDATE legal_holds SET retention_case_ids=ARRAY['${review}']::uuid[] WHERE id='${hold}';`);
  await run('dotnet',[dll,'retention','purge'],{cwd:apiDir,env});
  equal(await query(`SELECT count(*) FROM ban_check_attempts WHERE cardinality(matched_ban_ids)=0`),'0','expired unmatched operational quota records are purged');
  assert.ok(Number(await query(`SELECT count(*) FROM ban_check_attempts WHERE cardinality(matched_ban_ids)>0`))>0);checks++;
  await query(`UPDATE legal_holds SET status='Released',released_at=now(),release_reason='Fictional preservation test completed' WHERE id='${hold}';`);
  await run('dotnet',[dll,'retention','purge'],{cwd:apiDir,env});
  equal(await query(`SELECT count(*) FROM ban_check_attempts`),'0','release resumes due operational metadata expiry');
  equal(await query(`SELECT count(*) FROM banned_identifiers WHERE status='Active'`),'3','quota disposal does not erase active identifier restrictions');
  equal(await query(`SELECT count(*) FROM moderation_cases WHERE id='${review}' AND evidence_purged_at IS NOT NULL`),'1','registry retains minimal case provenance while expired original evidence follows its separate clock');
  const logs=await fs.readFile(path.join(folder,'first.log'),'utf8');
  assert.ok(!logs.includes(blocked.email)&&!logs.includes(allowed.email)&&!logs.includes('+61491570159')&&!logs.includes(hash));checks++;
  console.log(`PASS: isolated banned identifier enforcement (${checks} checks; local Keycloak authentication, API application registration; no external delivery).`);
 } finally {
  if(replica&&replica.exitCode===null){const exited=once(replica,'exit');replica.kill('SIGTERM');await exited;}
  await stop();
  await edge.close();if(created)await sql(`DROP DATABASE ${database} WITH (FORCE)`);await fs.rm(folder,{recursive:true,force:true});
 }
})().catch(error=>{console.error(error);process.exitCode=1;});
