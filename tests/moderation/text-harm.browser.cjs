// #115 actual source hooks, durable worker, restricted Admin human review. Fictional adults only.
const crypto=require('node:crypto'),fs=require('node:fs/promises'),path=require('node:path');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
const {managedEdge}=require('../auth/managed-edge.cjs');
const {web,admin,sleep,createMember,createStaff,memberToken,staffToken,rootAdminToken,cli,sql,call,claims,matchReady}=require('../auth/local-tokens.cjs');
if(!process.env.SWOOVR_TEST_API_BASE||!process.env.SWOOVR_TEST_APP_DATABASE||process.env.TextHarm__Enabled!=='true')throw Error('Use the owned runner with TextHarm__Enabled=true.');
let count=0,phase='setup';
const check=(ok,label)=>{if(!ok){const error=Error(label);error.safeLabel=true;throw error;}count++;};
async function until(test){for(let n=0;n<300;n++){if(await test())return true;await sleep(100);}return false;}
(async()=>{
 const edge=await managedEdge(chromium),browser=edge.browser,contexts=[];
 const folder=path.resolve(process.env.SWOOVR_SCREENSHOTS||'.local/suite-shots','2026-10-30-text-harm');await fs.mkdir(folder,{recursive:true});
 try{
  const root=await rootAdminToken(browser),people={},tokens={},ids={},publicIds={};
  for(const label of ['sender','peer'])people[label]=await createMember('text-'+label);
  await cli('beta','add',...Object.values(people).map(x=>x.email));
  for(const label of Object.keys(people)){
   tokens[label]=await memberToken(browser,people[label]);
   const result=await call(tokens[label],'/api/me/profile',{method:'PUT',body:{displayName:'Fictional text '+label,homeCityId:2078025,about:'Fictional ordinary profile'}});
   check(result.status===200,'ordinary profile write accepted');publicIds[label]=result.body.publicId;
   ids[label]=await sql(`SELECT id FROM accounts WHERE subject='${claims(tokens[label]).sub}'`);
   check((await call(tokens[label],'/api/me/modes/friends',{method:'PUT',body:{enabled:true,visible:true,shareCoreAbout:true}})).status===200,'ordinary mode saved');
   check((await matchReady(tokens[label])).every(s=>s===200),'fictional adult matching prerequisites');
  }
  const staff={},staffTokens={};
  for(const [label,role]of [['admin','admin'],['support','support'],['legal','legal']]){
   staff[label]=await createStaff('text-'+label);check((await call(root,'/api/admin/invitations',{method:'POST',body:{email:staff[label].email,role}})).status===201,'staff invited');
   staffTokens[label]=await staffToken(browser,staff[label]);await call(staffTokens[label],'/api/session');
  }
  phase='profile-source-hook';
  const text='A fictional quote for review: send me gift cards for dinner';
  const written=await call(tokens.sender,'/api/me/profile',{method:'PUT',body:{displayName:'Fictional text sender',homeCityId:2078025,about:text}});
  check(written.status===200&&written.body.about===text,'member profile response preserves submitted text');
  check(!('textHarm'in written.body)&&!('candidate'in written.body),'no accusation or detector verdict in member response');
  check(await until(async()=>await sql(`SELECT count(*) FROM text_harm_review WHERE subject_account_id='${ids.sender}'`)==='1'),'actual worker creates own-initiative profile candidate');
  const caseId=await sql(`SELECT case_id FROM text_harm_review WHERE subject_account_id='${ids.sender}' LIMIT 1`),base=`/api/admin/cases/${caseId}/text-candidates`;
  const list=await call(staffTokens.admin,base);check(list.status===200&&list.body.length===1,'admin gets candidate metadata');const reviewId=list.body[0].id;
  check(!('text'in list.body[0]),'list does not decrypt original text');
  check((await call(tokens.sender,base)).status===403,'member cannot inspect restricted candidates');
  check((await call(staffTokens.legal,base)).status===403,'legal-only role cannot use ordinary moderation review');
  check((await call(staffTokens.support,base)).status===200,'ordinary support can review Support queue');
  check((await call(staffTokens.admin,base+'/'+reviewId)).body.text===text,'restricted read returns exact encrypted original');
  check(await sql(`SELECT count(*) FROM reports WHERE case_id='${caseId}'`)==='0','own initiative creates no fabricated member report');
  check(await sql(`SELECT count(*) FROM sanctions WHERE account_id='${ids.sender}'`)==='0','detection imposes no sanction');
  check(await sql(`SELECT count(*) FROM member_notices WHERE account_id='${ids.sender}' AND kind='ContentRemoved'`)==='0','detection does not invent removal notice');
  check((await call(staffTokens.admin,base+'/'+reviewId,{method:'POST',body:{outcome:'actioned'}})).status===400,'actioned verdict cannot invent an action');
  check((await call(staffTokens.admin,`/api/admin/cases/${caseId}/close`,{method:'POST'})).status===409,'case cannot strand an unreviewed candidate by closing');
  phase='actual-admin-human-review';
  const context=await browser.newContext({viewport:{width:390,height:844}});contexts.push(context);const page=await context.newPage();
  await page.goto(admin+'/sign-in');await page.fill('#username',staff.admin.email);await page.fill('#password',staff.admin.password);
  await Promise.all([page.waitForURL(admin+'/'),page.click('#kc-login')]);
  await page.goto(admin+`/cases/${caseId}/text-review?candidate=${reviewId}`);await page.waitForSelector('[data-text-evidence]');
  check(await page.locator('[data-text-evidence]').innerText()===text,'real narrow Admin renders escaped original');
  check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'review fits narrow viewport');
  check((await page.locator('body').innerText()).includes('does not establish misconduct'),'candidate uncertainty is explicit');
  await page.screenshot({path:path.join(folder,'admin-text-candidate-390.png'),fullPage:true});
  await page.locator('input[name="ReviewForm.Confirmed"]').check();
  await Promise.all([page.waitForNavigation(),page.locator('[data-record-text-review]').click()]);
  check((await page.locator('[data-text-review-result]').innerText()).includes('Human review recorded'),'actual confirmed human no-action review succeeds');
  check(await sql(`SELECT outcome FROM text_harm_review WHERE id='${reviewId}'`)==='reviewed-no-action','human no-action outcome persisted');
  await page.screenshot({path:path.join(folder,'admin-text-reviewed-390.png'),fullPage:true});
  check((await call(staffTokens.admin,base+'/'+reviewId,{method:'POST',body:{outcome:'reviewed-no-action'}})).body.alreadyApplied===true,'same human outcome is idempotent');
  phase='mode-and-message-hooks';
  check((await call(tokens.sender,'/api/me/modes/friends',{method:'PUT',body:{enabled:true,visible:true,about:'Transfer me crypto for a fictional trip',shareCoreAbout:false}})).status===200,'per-mode source write accepted');
  check(await until(async()=>await sql(`SELECT count(*) FROM text_harm_review WHERE subject_account_id='${ids.sender}' AND source='ModeAbout' AND mode_id='friends'`)==='1'),'actual worker inspects exact per-mode source');
  const request=await call(tokens.sender,'/api/requests',{method:'POST',body:{to:publicIds.peer,mode:'friends'}});check(request.status===201,'request accepted');
  check((await call(tokens.peer,`/api/requests/${request.body.id}/accept`,{method:'POST'})).status===200,'peer accepts request');
  const conversation=await call(tokens.sender,'/api/conversations',{method:'POST',body:{to:publicIds.peer}});check([200,201].includes(conversation.status),'conversation opened');
  let last;
  for(let n=0;n<5;n++){last=await call(tokens.sender,`/api/conversations/${conversation.body.id}/messages`,{method:'POST',body:{text:'A fictional repeated invitation to dinner'}});check(last.status===201,'actual text message accepted');}
  check(await until(async()=>await sql(`SELECT count(*) FROM text_harm_review WHERE subject_account_id='${ids.sender}' AND source_id='${last.body.id}'`)==='1'),'fifth actual repeated message creates review candidate');
  const messageReview=await sql(`SELECT id FROM text_harm_review WHERE source_id='${last.body.id}'`);
  const read=await call(staffTokens.admin,base+'/'+messageReview);check(read.status===200&&read.body.rules.includes('repetitive-message-threshold'),'actual server history drives repeated-message rule');
  const report=await call(tokens.peer,'/api/reports',{method:'POST',body:{subjectType:'message',subjectId:last.body.id,category:'harassment',text:'Fictional human report for removal test',block:false}});check(report.status===201,'a real member can separately report candidate message');
  const sources=await call(staffTokens.admin,`/api/admin/cases/${caseId}/content-sources`),source=sources.body.find(s=>s.messageId===last.body.id);
  check(!!source,'reported current message is eligible for explicit content removal');
  const removal=await call(staffTokens.admin,`/api/admin/cases/${caseId}/content-removals`,{method:'POST',body:{commandId:crypto.randomUUID(),reportId:report.body.id,field:'message',messageId:last.body.id,expectedSourceSha256:source.sha256,reason:'Fictional human reviewed removal'}});
  check(removal.status===200,'human removes exact reported content through existing workflow');
  const actioned=await call(staffTokens.admin,base+'/'+messageReview,{method:'POST',body:{outcome:'actioned',removalId:removal.body.id}});
  check(actioned.status===200&&actioned.body.outcome==='actioned','human action outcome links actual matching removal');
  phase='urgent-candidate';
  const threat=await call(tokens.peer,`/api/conversations/${conversation.body.id}/messages`,{method:'POST',body:{text:'Fictional test quote: I will kill you'}});check(threat.status===201,'threat phrase is submitted for human review');
  check(await until(async()=>await sql(`SELECT count(*) FROM text_harm_review WHERE source_id='${threat.body.id}'`)==='1'),'actual worker creates urgent threat candidate');
  const urgentCase=await sql(`SELECT case_id FROM text_harm_review WHERE source_id='${threat.body.id}'`);
  check(await sql(`SELECT queue||':'||priority FROM moderation_cases WHERE id='${urgentCase}'`)==='Admin:Urgent','threat candidate routes to urgent Admin queue');
  check(await sql(`SELECT count(*) FROM legal_holds WHERE case_id='${urgentCase}' AND source='text-harm-threat' AND status='Provisional'`)==='1','threat candidate has actual provisional preservation');
  check(await sql(`SELECT count(*) FROM sanctions WHERE account_id='${ids.peer}'`)==='0','urgent candidate is still not an automatic sanction');
  check((await call(staffTokens.support,`/api/admin/cases/${urgentCase}/text-candidates`)).status===403,'Support cannot decrypt urgent Admin candidates');
  console.log(`PASS: text harm hooks and human review (${count} checks; owned PostgreSQL/API/worker/Admin, fictional content, no measured accuracy/provider delivery claim).`);
 }finally{for(const context of contexts)await context.close();await edge.close();}
})().catch(error=>{console.error('FAIL text review phase '+phase+': '+(error.safeLabel?error.message:'inspection required; raw content withheld'));process.exitCode=1;});
