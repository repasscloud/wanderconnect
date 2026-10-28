const {expireFictionalReviews}=require('../retention/fixtures.cjs');
// Issue128: deterministic stale-review race with synthetic local files and two actual API processes.
const assert=require('node:assert/strict');const fs=require('node:fs/promises');const os=require('node:os');const path=require('node:path');
const {spawn,execFile}=require('node:child_process');const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
const {createMember,createStaff,cli,memberToken,staffToken,rootAdminToken,call,claims,matchReady,sql,sleep}=require('../auth/local-tokens.cjs');
const {synthetic,upload}=require('./synthetic-images.cjs');
async function stop(port){await new Promise(r=>execFile('bash',['-c',`lsof -ti tcp:${port} -sTCP:LISTEN | xargs kill 2>/dev/null; true`],()=>r()));for(let i=0;i<30;i++){try{await fetch(`http://127.0.0.1:${port}/health/live`);await sleep(200);}catch{return;}}}
async function start(port,env,backups){await stop(port);try{await fs.copyFile(`.local/logs/api-${port}.log`,path.join(backups,`api-${port}-${Date.now()}.log`));}catch(e){if(e.code!=='ENOENT')throw e;}
 await new Promise((resolve,reject)=>{const p=spawn('bash',['.local/run-hosts.sh'],{env:{...process.env,SWOOVR_EXTRA_API_PORT:String(port),Phone__Required:'false',Accounts__ReauthSeconds:'45',...env},stdio:['ignore','pipe','pipe']});p.on('exit',n=>n===0?resolve():reject(Error('Extra transition API did not start')));});}
async function waitFor(check,attempts=160){for(let i=0;i<attempts;i++){if(await check())return;await sleep(100);}throw Error('Transition state timed out');}
async function decision(port,token,id,choice){const r=await fetch(`http://127.0.0.1:${port}/api/admin/media/${id}/decision`,{method:'POST',headers:{Authorization:'Bearer '+token,'Swoovr-Version':'2026-10-28','Content-Type':'application/json'},body:JSON.stringify({decision:choice,reason:'Fictional concurrency acceptance'})});return{status:r.status,body:await r.json()};}
(async()=>{
 const folder=await fs.mkdtemp(path.join(os.tmpdir(),'swoovr-transition-'));const backups=await fs.mkdtemp(path.join(os.tmpdir(),'swoovr-transition-log-backups-'));
 const browser=await chromium.launch({channel:'msedge',headless:true});const canvas=await browser.newPage();let account,legal,fake,scanReply;let passed=0;const pass=()=>passed++;
 try{
  const root=await rootAdminToken(browser),member=await createMember('photo-transition'),officer=await createStaff('transition-legal');
  await cli('beta','add',member.email);const token=await memberToken(browser,member);
  await call(token,'/api/me/profile',{method:'PUT',body:{displayName:'Fictional Transition Tester',homeCityId:2078025}});await matchReady(token);
  account=await sql(`SELECT id FROM accounts WHERE subject='${claims(token).sub}'`);
  assert.equal((await call(root,'/api/admin/invitations',{method:'POST',body:{email:officer.email,role:'legal'}})).status,201);
  legal=await staffToken(browser,officer);await call(legal,'/api/session');
  const env={Media__Store:'local',Media__Local__Path:folder,Media__Screening__Simulate:'uncertain'};
  await start(5090,{...env,Media__Publication__BarrierDirectory:path.join(folder,'barriers'),Media__Publication__BarrierStage:'after-read'},backups);
  await start(5091,{...env,Media__Screening__Worker:'false'},backups);
  const up=await upload('http://127.0.0.1:5090/api/me/photos',await synthetic(canvas,{kind:'gradient',type:'image/jpeg',seed:701}),{token});assert.equal(up.status,202);const id=up.body.id;pass();
  await waitFor(async()=>await sql(`SELECT status FROM media_items WHERE id='${id}'`)==='Uncertain');
  const approval=decision(5090,root,id,'approve');
  await waitFor(async()=>{try{await fs.access(path.join(folder,'barriers',id+'.entered'));return true;}catch{return false;}});pass();
  const block=decision(5091,root,id,'escalate');
  // Old code completes block while approval holds raw bytes. Serialized code waits until approval's lease releases.
  await Promise.race([block,sleep(3000)]);
  await fs.writeFile(path.join(folder,'barriers',id+'.release'),'');
  const [approved,blocked]=await Promise.all([approval,block]);assert.equal(blocked.status,200,JSON.stringify(blocked.body));pass();
  assert.ok([200,409,404].includes(approved.status));pass();
  assert.equal(await sql(`SELECT status FROM media_items WHERE id='${id}'`),'Blocked','a committed blocked verdict must never be overwritten by stale approval');pass();
  assert.equal(await sql(`SELECT count(*) FROM media_placements WHERE media_id='${id}'`),'0');pass();
  await waitFor(async()=>await sql(`SELECT count(*) FROM media_items WHERE id='${id}' AND preservation_completed_at IS NOT NULL`)==='1');pass();
  await assert.rejects(fs.access(path.join(folder,'photos',id.replaceAll('-',''))));pass();
  // Reverse ordering: an approval already holding an Uncertain snapshot waits before acquiring its lease.
  await start(5090,{...env,Media__Publication__BarrierDirectory:path.join(folder,'barriers'),Media__Publication__BarrierStage:'before-lease'},backups);
  const stale=await upload('http://127.0.0.1:5090/api/me/photos',await synthetic(canvas,{kind:'gradient',type:'image/jpeg',seed:702}),{token});
  assert.equal(stale.status,202);const staleId=stale.body.id;
  await waitFor(async()=>await sql(`SELECT status FROM media_items WHERE id='${staleId}'`)==='Uncertain');
  const late=decision(5090,root,staleId,'approve');
  await waitFor(async()=>{try{await fs.access(path.join(folder,'barriers',staleId+'.entered'));return true;}catch{return false;}});
  await fs.mkdir(path.join(folder,'photos'),{recursive:true});
  await fs.copyFile(path.join(folder,'quarantine',staleId.replaceAll('-','')),path.join(folder,'photos',staleId.replaceAll('-','')));
  assert.equal((await decision(5091,root,staleId,'escalate')).status,200);pass();
  await fs.writeFile(path.join(folder,'barriers',staleId+'.release'),'');
  assert.equal((await late).status,409,'stale approval must report refusal');pass();
  assert.equal(await sql(`SELECT status FROM media_items WHERE id='${staleId}'`),'Blocked');pass();
  assert.equal(await sql(`SELECT count(*) FROM media_placements WHERE media_id='${staleId}'`),'0');pass();
  await assert.rejects(fs.access(path.join(folder,'photos',staleId.replaceAll('-',''))));pass();
  // A member removal remains responsive while screening owns the source. It never publishes afterward.
  await start(5090,{...env,Media__Publication__BarrierDirectory:path.join(folder,'barriers'),Media__Publication__BarrierStage:'after-read'},backups);
  const removed=await upload('http://127.0.0.1:5090/api/me/photos',await synthetic(canvas,{kind:'gradient',type:'image/jpeg',seed:703}),{token});
  assert.equal(removed.status,202);const removedId=removed.body.id;
  await waitFor(async()=>await sql(`SELECT status FROM media_items WHERE id='${removedId}'`)==='Uncertain');
  const removedApproval=decision(5090,root,removedId,'approve');
  await waitFor(async()=>{try{await fs.access(path.join(folder,'barriers',removedId+'.entered'));return true;}catch{return false;}});
  const removal=await fetch(`http://127.0.0.1:5091/api/me/photos/${removedId}`,{method:'DELETE',headers:{Authorization:'Bearer '+token,'Swoovr-Version':'2026-10-28'}});
  assert.equal(removal.status,200);pass();
  assert.equal(await sql(`SELECT count(*) FROM media_items WHERE id='${removedId}' AND removed_at IS NOT NULL`),'1');pass();
  await fs.writeFile(path.join(folder,'barriers',removedId+'.release'),'');
  assert.equal((await removedApproval).status,409);pass();
  await waitFor(async()=>await sql(`SELECT status FROM media_items WHERE id='${removedId}'`)==='Removed');pass();
  assert.equal(await sql(`SELECT count(*) FROM media_placements WHERE media_id='${removedId}'`),'0');pass();
  await assert.rejects(fs.access(path.join(folder,'photos',removedId.replaceAll('-',''))));pass();
  // Two automatic workers contend for the same pending source; only the first classifies it.
  let scanSeen=false;
  fake=require('node:http').createServer((req,res)=>{
   req.resume();
   if(req.url==='/ready'){res.writeHead(200).end('{}');return;}
   if(req.url!=='/v1/scan'){res.writeHead(404).end();return;}
   scanSeen=true;scanReply=()=>res.writeHead(200,{'Content-Type':'application/json'}).end(JSON.stringify({status:'completed',results:{nudenet:{status:'completed',detections:[]},nsfw:{status:'completed',is_nsfw:false},arachnid:{status:'disabled'}}}));
  });
  await new Promise(r=>fake.listen(0,'127.0.0.1',r));
  await start(5090,{...env,Media__Screening__Simulate:'',Media__Screening__BaseUrl:`http://127.0.0.1:${fake.address().port}`,Media__Screening__RequireKnownContentCheck:'false'},backups);
  const competing=await upload('http://127.0.0.1:5090/api/me/photos',await synthetic(canvas,{kind:'gradient',type:'image/jpeg',seed:704}),{token});
  assert.equal(competing.status,202);const competingId=competing.body.id;
  await waitFor(()=>scanSeen);pass();
  await sql(`UPDATE media_items SET next_screen_at=now()-interval '1 minute' WHERE id='${competingId}'`);
  await start(5091,{...env,Media__Screening__Worker:'true'},backups);
  await waitFor(async()=>Number(await sql("SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND wait_event='advisory'"))>0,450);pass();
  scanReply();scanReply=null;
  await waitFor(async()=>await sql(`SELECT status FROM media_items WHERE id='${competingId}'`)==='Passed');
  assert.equal(await sql(`SELECT screen_attempts FROM media_items WHERE id='${competingId}'`),'1');pass();
  assert.equal((await decision(5091,root,competingId,'escalate')).status,200);pass();
  assert.equal(await sql(`SELECT status FROM media_items WHERE id='${competingId}'`),'Blocked');pass();
  assert.equal(await sql(`SELECT count(*) FROM media_placements WHERE media_id='${competingId}'`),'0');pass();
  await assert.rejects(fs.access(path.join(folder,'photos',competingId.replaceAll('-',''))));pass();
  console.log(`PASS: media transition concurrency (${passed} checks; synthetic local files only).`);
 }finally{
  if(scanReply)scanReply();
  await stop(5090);await stop(5091);
  if(fake)await new Promise(r=>fake.close(r));
  if(account&&legal){const holds=await sql(`SELECT h.id FROM legal_holds h JOIN legal_hold_scopes s ON s.hold_id=h.id WHERE s.target_id='${account}' AND h.status<>'Released'`);
   for(const id of holds.split('\n').filter(Boolean))assert.equal((await call(legal,`/api/admin/holds/${id}/release`,{method:'POST',body:{reason:'Fictional synthetic concurrency test complete.',overlapsChecked:true}})).status,200);
   await expireFictionalReviews([account]);
   await sql(`DELETE FROM media_items WHERE account_id='${account}'`);}
  await fs.rm(folder,{recursive:true,force:true});await browser.close();
 }
})().catch(e=>{console.error(e);process.exit(1)});
