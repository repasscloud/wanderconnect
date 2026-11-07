// D297/D298: owned synthetic local media metadata/bytes; this is not a provider screening accuracy test.
const assert=require('node:assert/strict'),crypto=require('node:crypto'),fs=require('node:fs/promises'),path=require('node:path'),os=require('node:os');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
const h=require('../auth/local-tokens.cjs'),{synthetic}=require('./synthetic-images.cjs');
if(!process.env.SWOOVR_TEST_APP_DATABASE||!process.env.SWOOVR_TEST_API_BASE||process.env.Media__Store!=='local'||!process.env.Media__Local__Path?.startsWith(os.tmpdir()))throw Error('Photo workflow acceptance requires owned local media/database');
if(process.env.Media__Screening__Simulate!=='blocked')throw Error('Photo workflow suite requires explicit labelled blocked simulation for specialist re-screen proof');
(async()=>{
 const edge=await require('../auth/managed-edge.cjs').managedEdge(chromium);
 try{
  const root=await h.rootAdminToken(edge.browser),member=await h.createMember('photo-decisions'),people={support:await h.createStaff('photo-support'),first:await h.createStaff('photo-first'),second:await h.createStaff('photo-second')};
  for(const[key,person]of Object.entries(people))assert.equal((await h.call(root,'/api/admin/invitations',{method:'POST',body:{email:person.email,role:key==='support'?'support':'admin'}})).status,201);
  const tokens={};for(const[key,person]of Object.entries(people)){tokens[key]=await h.staffToken(edge.browser,person);await h.call(tokens[key],'/api/session');}
  const token=await h.memberToken(edge.browser,member);await h.cli('beta','add',member.email);await h.call(token,'/api/session');
  const account=await h.sql(`SELECT id FROM accounts WHERE subject='${h.claims(token).sub}'`),context=await edge.browser.newContext(),page=await context.newPage();
  try{
   const bytes=await synthetic(page,{type:'image/jpeg',seed:159}),hash=crypto.createHash('sha256').update(bytes).digest('hex'),ids=Array.from({length:51},()=>crypto.randomUUID());
   await fs.mkdir(path.join(process.env.Media__Local__Path,'quarantine'),{recursive:true});
   for(const id of ids){const key='quarantine/'+id.replaceAll('-','');await fs.writeFile(path.join(process.env.Media__Local__Path,key),bytes,{mode:0o600});await h.sql(`INSERT INTO media_items(id,account_id,status,object_key,sha256,upload_sha256,content_type,width,height,bytes,created_at,screen_attempts,screened_at,screener,known_content_checked,signals) VALUES('${id}','${account}','Uncertain','${key}',decode('${hash}','hex'),decode('${hash}','hex'),'image/jpeg',640,480,${bytes.length},now(),1,now(),'synthetic-fixture',false,'{"reason":"synthetic-fixture"}')`);}
   const ack=t=>h.call(t,'/api/admin/media/safety-ack',{method:'POST',body:{version:'2026-11-02',confirmed:true}});
   assert.equal((await h.call(token,'/api/admin/media/workload')).status,403,'member cannot see workload');
   assert.equal((await h.call(tokens.support,`/api/admin/media/${ids[0]}/view`,{method:'POST',body:{purpose:'Fictional routine review'}})).status,403,'server refuses reveal without acknowledgement');
   assert.equal((await h.call(tokens.support,'/api/admin/media/safety-ack',{method:'POST',body:{version:'old',confirmed:true}})).status,400);
   for(const t of Object.values(tokens))assert.equal((await ack(t)).status,200);
   const first=(await h.call(tokens.support,'/api/admin/media/review?page=1')).body,second=(await h.call(tokens.support,'/api/admin/media/review?page=2')).body;
   assert.equal(first.total,51);assert.equal(first.items.length,25);assert.equal(second.items.length,25);assert.ok(!first.items.some(a=>second.items.some(b=>a.id===b.id)));
   await h.sql(`UPDATE member_profiles SET employee=true WHERE account_id='${account}'`);
   assert.equal((await h.call(tokens.support,'/api/admin/media/review')).body.total,0,'Support queue excludes staff-affiliated subjects');
   assert.equal((await h.call(tokens.support,'/api/admin/media/workload')).body.manualReview,0,'Support workload uses the same affiliation policy');
   assert.equal((await h.call(tokens.support,`/api/admin/media/${ids[0]}/view`,{method:'POST',body:{purpose:'Fictional excluded review'}})).status,404);
   assert.equal((await h.call(tokens.support,`/api/admin/media/${ids[0]}/decision`,{method:'POST',body:{decision:'reject',reasonCode:'REJECT_PRIVACY'}})).status,404,'direct Support mutation cannot bypass affiliation exclusion');
   assert.equal((await h.call(tokens.first,'/api/admin/media/review')).body.total,51,'Admin retains routine access to unrelated staff-affiliated accounts');
   await h.sql(`UPDATE member_profiles SET employee=false WHERE account_id='${account}'`);

   const reviewerId=await h.sql(`SELECT id FROM accounts WHERE subject='${h.claims(tokens.support).sub}'`);
   await h.sql(`UPDATE media_safety_acknowledgements SET acknowledged_at=now()-interval '4 hours' WHERE staff_account_id='${reviewerId}'`);
   assert.equal((await h.call(tokens.support,`/api/admin/media/${ids[0]}/decision`,{method:'POST',body:{decision:'reject',reasonCode:'REJECT_PRIVACY'}})).status,403,'expired ack cannot decide');await ack(tokens.support);
   const rejected=await h.call(tokens.first,`/api/admin/media/${ids[0]}/decision`,{method:'POST',body:{decision:'reject',reasonCode:'REJECT_OTHER',reason:'Private fictional reviewer note'}});assert.equal(rejected.status,200);
   assert.ok(await fs.stat(path.join(process.env.Media__Local__Path,'quarantine',ids[0].replaceAll('-',''))),'rejection retains source privately');
   assert.equal((await h.call(token,'/api/me/photos')).body.photos.find(p=>p.id===ids[0]).link,null);
   let decisions=(await h.call(token,'/api/me/photos/decisions')).body;let decision=decisions.find(d=>d.mediaId===ids[0]);assert.equal(decision.canAppeal,true);assert.ok(!JSON.stringify(decision).includes('Private fictional'));
   const appeal=await h.call(token,`/api/me/photos/decisions/${decision.id}/appeal`,{method:'POST',body:{text:'Fictional appeal explanation'}});assert.equal(appeal.status,201);
   assert.equal((await h.call(token,`/api/me/photos/decisions/${decision.id}/appeal`,{method:'POST',body:{text:'Duplicate'}})).status,409);
   assert.equal((await h.call(tokens.support,'/api/admin/media/appeals')).status,403,'Support cannot decide appeals');
   const resolve=(t,outcome)=>h.call(t,`/api/admin/media/appeals/${appeal.body.id}/resolve`,{method:'POST',body:{outcome,reason:'Fictional independent review rationale'}});
   assert.equal((await resolve(tokens.first,'uphold')).status,403,'original reviewer cannot decide appeal');assert.equal((await resolve(tokens.second,'uphold')).status,200);
   assert.equal((await resolve(tokens.second,'uphold')).status,409,'final outcome is not replayable');
   assert.equal((await h.call(tokens.second,`/api/admin/media/${ids[0]}/reconsider`,{method:'POST',body:{outcome:'approve',reason:'Cannot bypass final appeal'}})).status,409);
   decisions=(await h.call(token,'/api/me/photos/decisions')).body;assert.equal(decisions.find(d=>d.id===decision.id).appealOutcome,'upheld');
   assert.equal(await h.sql(`SELECT count(*) FROM notifications WHERE account_id='${account}' AND kind='PhotoDecision'`),'2','one rejection and one final outcome notice');
   // Specialist rows expose only generic wording and never an appeal action; no real illegal media is used.
   const protectedDecision=crypto.randomUUID();await h.sql(`UPDATE media_items SET status='Blocked' WHERE id='${ids[1]}';INSERT INTO media_decision_records(id,media_id,account_id,decision,code,at)VALUES('${protectedDecision}','${ids[1]}','${account}','escalate','SPECIALIST_RESTRICTED',now())`);
   decision=(await h.call(token,'/api/me/photos/decisions')).body.find(d=>d.id===protectedDecision);assert.equal(decision.canAppeal,false);assert.equal(decision.decision,'not-approved');assert.equal(decision.reason,'This photo cannot be used on Swoovr.');
   assert.equal((await h.call(token,`/api/me/photos/decisions/${protectedDecision}/appeal`,{method:'POST',body:{text:'Not eligible'}})).status,409);
   // Build an earlier approved revision as synthetic historical setup; then exercise the actual newer rejection.
   const reject=(id)=>h.call(tokens.first,`/api/admin/media/${id}/decision`,{method:'POST',body:{decision:'reject',reasonCode:'REJECT_PRIVACY'}});
   assert.equal((await reject(ids[3])).status,200);const old=(await h.call(token,'/api/me/photos/decisions')).body.find(d=>d.mediaId===ids[3]);
   await h.sql(`INSERT INTO media_decision_records(id,media_id,account_id,actor_id,decision,code,at)VALUES('${crypto.randomUUID()}','${ids[3]}','${account}',NULL,'approve','APPROVED_AFTER_REVIEW',now());UPDATE media_items SET status='Passed' WHERE id='${ids[3]}'`);
   assert.equal((await reject(ids[3])).status,200);const revisions=(await h.call(token,'/api/me/photos/decisions')).body.filter(d=>d.mediaId===ids[3]);
   assert.equal(await h.sql(`SELECT count(*) FROM moderation_transparency_facts WHERE source_kind='media-decision' AND source_id='${ids[3]}'`),'2','each rejection keeps an independent immutable action receipt');
   assert.equal(revisions.find(d=>d.id===old.id).canAppeal,false,'superseded rejection cannot advertise an appeal');
   const newest=revisions.find(d=>d.canAppeal);assert.ok(newest&&newest.id!==old.id);
   assert.equal((await h.call(token,`/api/me/photos/decisions/${old.id}/appeal`,{method:'POST',body:{text:'Old rejection'}})).status,409,'direct stale decision appeal refused');
   const nextAppeal=await h.call(token,`/api/me/photos/decisions/${newest.id}/appeal`,{method:'POST',body:{text:'Newest rejection'}});assert.equal(nextAppeal.status,201);
   assert.equal((await h.call(tokens.first,'/api/admin/media/workload')).body.appeals,0);assert.equal((await h.call(tokens.first,'/api/admin/media/appeals')).body.total,0,'original reviewer workload and queue agree');
   // Issue 232: the original reviewer is told the appeal waits for another Admin instead of seeing an empty queue.
   assert.equal((await h.call(tokens.first,'/api/admin/media/workload')).body.awaitingOtherAdmin,1);assert.equal((await h.call(tokens.first,'/api/admin/media/appeals')).body.awaitingOtherAdmin,1,'original reviewer sees it waits for another Admin');
   assert.equal((await h.call(tokens.second,'/api/admin/media/workload')).body.awaitingOtherAdmin,0);
   assert.equal((await h.call(tokens.second,'/api/admin/media/workload')).body.appeals,1);assert.equal((await h.call(tokens.second,'/api/admin/media/appeals')).body.total,1);
   const specialist=await h.call(tokens.second,`/api/admin/media/appeals/${nextAppeal.body.id}/resolve`,{method:'POST',body:{outcome:'approve',reason:'Fictional fresh re-screen with labelled blocked simulation'}});
   assert.equal(specialist.status,200);assert.equal(specialist.body.outcome,'unavailable','specialist result closes appeal generically');
   assert.equal(await h.sql(`SELECT outcome FROM media_appeals WHERE id='${nextAppeal.body.id}' AND decided_at IS NOT NULL`),'unavailable');
   assert.equal((await h.call(tokens.second,'/api/admin/media/workload')).body.appeals,0);assert.equal((await h.call(tokens.second,'/api/admin/media/appeals')).body.total,0);
   assert.equal((await h.call(tokens.first,'/api/admin/media/workload')).body.awaitingOtherAdmin,0,'decided appeal no longer waits for another Admin');
   assert.ok(!(await h.call(token,'/api/me/photos/decisions')).body.filter(d=>d.mediaId===ids[3]).some(d=>d.canAppeal));
   await page.goto(h.admin+'/sign-in');await page.fill('#username',people.support.email);await page.fill('#password',people.support.password);await page.click('#kc-login');await page.waitForURL(h.admin+'/**');await page.goto(h.admin+'/media');await page.locator('[data-media-item]').first().locator('[data-review-open]').click();
   await page.waitForSelector('dialog:modal');await page.keyboard.press('Escape');await page.waitForSelector('[data-photo-dialog]',{state:'detached'});
   console.log('PASS: owned photo review paging/roles/acknowledgement, retained rejection, safe durable notices, one independent final appeal, specialist exclusion and keyboard dialog');
  }finally{await context.close();}
 }finally{await edge.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
