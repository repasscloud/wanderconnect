// Actual hosted coordinator + restart against a throwaway PostgreSQL database and synthetic local files.
// No owner database mutation, R2, screening, email, SMS or device provider is used by this host.
const assert=require('node:assert/strict');
const {execFile,spawn}=require('node:child_process');
const {once}=require('node:events');
const net=require('node:net');
const fs=require('node:fs/promises'),path=require('node:path'),os=require('node:os'),crypto=require('node:crypto');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
const {sql,localEnv,createMember,memberToken,claims}=require('../auth/local-tokens.cjs');
const run=(cmd,args,options={})=>new Promise((resolve,reject)=>execFile(cmd,args,options,(error,stdout,stderr)=>error?reject(new Error(`${cmd} failed: ${stderr.slice(-800)}`)):resolve(stdout.trim())));
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
(async()=>{
 const database='swoovr_retention_'+crypto.randomUUID().replaceAll('-',''),folder=await fs.mkdtemp(path.join(os.tmpdir(),'swoovr-retention-worker-'));
 const root=process.cwd(),apiDir=path.join(root,'src/Swoovr.Api'),dll=path.join(apiDir,'bin/Debug/net10.0/Swoovr.Api.dll');
 const portProbe=net.createServer();await new Promise((resolve,reject)=>{portProbe.once('error',reject);portProbe.listen(0,'127.0.0.1',resolve)});
 const base='http://127.0.0.1:'+portProbe.address().port;await new Promise(resolve=>portProbe.close(resolve));
 const edge=await require('../auth/managed-edge.cjs').managedEdge(chromium),browser=edge.browser;let host,created=false,checks=0;
 const query=text=>run('docker',['exec','swoovr-local-postgres-1','psql','-U','swoovr','-d',database,'-v','ON_ERROR_STOP=1','-tAc',text]);
 const equal=(a,b,label)=>{assert.equal(a,b,label);checks++;};
 const stop=async()=>{if(!host)return;const current=host;host=null;const exited=once(current,'exit');current.kill('SIGTERM');await Promise.race([exited,sleep(4000)]);if(current.exitCode===null){current.kill('SIGKILL');await exited;}};
 try{
  await sql(`CREATE DATABASE ${database}`);created=true;
  const env={...process.env,DOTNET_ENVIRONMENT:'Development',ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:base,
   ConnectionStrings__Swoovr:`Host=127.0.0.1;Port=5433;Database=${database};Username=swoovr;Password=${await localEnv('SWOOVR_DB_PASSWORD')}`,
   Founders__Secrets__1:await localEnv('SWOOVR_FOUNDER_SECRET_V1'),Media__Store:'local',Media__Local__Path:folder,Media__Screening__Worker:'false',Retention__Worker:'false'};
  await run('dotnet',[dll,'migrate'],{cwd:apiDir,env});
  const start=async label=>{
   const logfile=path.join(folder,label+'.log'),handle=await fs.open(logfile,'w');
   host=spawn('dotnet',[dll],{cwd:apiDir,env,stdio:['ignore',handle.fd,handle.fd]});await handle.close();
   for(let n=0;n<100;n++){
    if(host.exitCode!==null)throw new Error('Isolated retention host exited; inspect local temporary log.');
    try{if((await fetch(base+'/health/live')).ok)return logfile;}catch{}
    await sleep(100);
   }throw new Error('Isolated retention host did not start.');
  };
  await start('seed');
  const accounts=[];
  for(const label of ['worker-a','worker-b']){
   const token=await memberToken(browser,await createMember(label));
   const response=await fetch(base+'/api/session',{headers:{Authorization:'Bearer '+token,'Swoovr-Version':'2026-10-28'}});
   equal(response.status,200,'validated local Keycloak session on isolated host');
   accounts.push(await query(`SELECT id FROM accounts WHERE subject='${claims(token).sub}'`));
  }
  const [a,b]=accounts,review=crypto.randomUUID(),report=crypto.randomUUID(),media=crypto.randomUUID(),conversation=crypto.randomUUID(),message=crypto.randomUUID();
  const objectKey='photos/'+media.replaceAll('-','');await fs.mkdir(path.join(folder,'photos'));await fs.writeFile(path.join(folder,objectKey),'Fictional worker disposal fixture; never rendered.');
  await query(`INSERT INTO moderation_cases(id,subject_account_id,status,queue,priority,created_at,updated_at,closed_at)
   VALUES('${review}','${a}','Closed','Support','Normal',now()-interval '30 months',now()-interval '30 months',now()-interval '30 months');
   INSERT INTO reports(id,reporter_account_id,subject_account_id,subject_type,subject_ref,category_id,priority,text,case_id,created_at)
   VALUES('${report}','${b}','${a}','Profile','${a}','other','Normal','Fictional expired review','${review}',now()-interval '30 months');
   INSERT INTO case_evidence(id,case_id,report_id,kind,snapshot,captured_at,media_ids)
   VALUES('${crypto.randomUUID()}','${review}','${report}','profile','{}',now()-interval '30 months',ARRAY['${media}']::uuid[]);
   INSERT INTO media_items(id,account_id,status,sha256,upload_sha256,content_type,width,height,bytes,created_at,screen_attempts,known_content_checked,purpose,removed_at,object_key)
   VALUES('${media}','${a}','Removed',decode(repeat('00',32),'hex'),decode(repeat('00',32),'hex'),'image/jpeg',1,1,50,now()-interval '30 months',0,false,'profile',now()-interval '29 months','${objectKey}');
   INSERT INTO conversations(id,pair_low,pair_high,created_at,last_message_at)
   VALUES('${conversation}',least('${a}'::uuid,'${b}'::uuid),greatest('${a}'::uuid,'${b}'::uuid),now()-interval '50 days',now()-interval '40 days');
   INSERT INTO messages(id,conversation_id,sender_account_id,kind,body,sent_at,deleted_at)
   VALUES('${message}','${conversation}','${a}','Text','Fictional ordinary expired text',now()-interval '40 days',now()-interval '31 days');`);
  const expiryCase=crypto.randomUUID(),expiryBan=crypto.randomUUID();
  await query(`INSERT INTO moderation_cases(id,subject_account_id,status,queue,priority,created_at,updated_at,closed_at)
   VALUES('${expiryCase}','${a}','Closed','Admin','Normal',now(),now(),now());
   INSERT INTO banned_identifiers(id,identifier_type,identifier_hash,key_version,case_id,ban_reason_code,status,banned_at,ban_expires_at,match_count)
   VALUES('${expiryBan}','Email',decode(repeat('31',32),'hex'),1,'${expiryCase}','permanent-account-ban','Active',now()-interval '2 days',now()-interval '1 day',0);`);
  await stop();env.Retention__Worker='true';env.Evidence__HoldCheckUnavailable='true';const closedLog=await start('closed');
  for(let n=0;n<120;n++){
   if((await fs.readFile(closedLog,'utf8')).includes('Retention run stopped'))break;
   if(n===119)throw new Error('Unavailable-hold worker did not report its refusal.');await sleep(500);
  }
  equal(await query(`SELECT count(*) FROM moderation_cases WHERE id='${review}'`),'1','unavailable hold status preserves review records');
  equal(await query(`SELECT body FROM messages WHERE id='${message}'`),'Fictional ordinary expired text','unavailable hold status stops later message phase');
  await fs.access(path.join(folder,objectKey));checks++;
  equal(await query(`SELECT status FROM banned_identifiers WHERE id='${expiryBan}'`),'Active','unavailable preservation stops later expiry reconciliation');
  await stop();env.Evidence__HoldCheckUnavailable='false';await start('first');
  for(let n=0;n<120;n++){
   if(await query(`SELECT count(*) FROM audit_entries WHERE action='case.records-expired' AND target_id='${review}'`)==='1'
    && await query(`SELECT body='' FROM messages WHERE id='${message}'`)==='t'
    && await query(`SELECT count(*) FROM media_items WHERE id='${media}'`)==='0')break;
   if(n===119)throw new Error('Hosted retention coordinator did not finish within 60 seconds.');await sleep(500);
  }
  equal(await query(`SELECT count(*) FROM moderation_cases WHERE id='${review}'`),'0','worker disposes expired case records');
  equal(await query(`SELECT body FROM messages WHERE id='${message}'`),'','worker shares ordinary message expiry');
  await assert.rejects(fs.access(path.join(folder,objectKey)));checks++;
  equal(await query(`SELECT count(*) FROM audit_entries WHERE action='case.content-expired' AND target_id='${review}'`),'1','one content disposal audit');
  equal(await query(`SELECT count(*) FROM accounts WHERE status='Active'`),'2','no provider/account deletion required');
  await stop();const restartLog=await start('restart');
  for(let n=0;n<120;n++){
   if((await fs.readFile(restartLog,'utf8')).includes('Retention:'))break;
   if(n===119)throw new Error('Restarted worker did not run.');await sleep(500);
  }
  equal(await query(`SELECT count(*) FROM audit_entries WHERE action='case.content-expired' AND target_id='${review}'`),'1','restart does not duplicate completed work');
  equal(await query(`SELECT count(*) FROM audit_entries WHERE action='case.records-expired' AND target_id='${review}'`),'1','record disposal stays idempotent');
  equal(await query(`SELECT status FROM banned_identifiers WHERE id='${expiryBan}'`),'Expired','hosted coordinator reconciles due restriction');
  equal(await query(`SELECT count(*) FROM ban_transitions WHERE banned_identifier_id='${expiryBan}' AND action='expired' AND actor_account_id IS NULL`),'1','restart appends one system expiry event');
  equal(await query(`SELECT count(*) FROM ban_case_notes n JOIN ban_transitions t ON n.transition_id=t.id WHERE t.banned_identifier_id='${expiryBan}' AND n.case_id='${expiryCase}'`),'1','hosted expiry retains linked case note');
  console.log(`PASS: hosted retention worker (${checks} checks; throwaway PostgreSQL, local synthetic files, actual restart; no external delivery).`);
 }finally{
  await stop();await edge.close();if(created)await sql(`DROP DATABASE ${database} WITH (FORCE)`);await fs.rm(folder,{recursive:true,force:true});
 }
})().catch(error=>{console.error(error);process.exitCode=1});
