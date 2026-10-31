// #117 real transactional report outcomes, generic own centre and routine-report support. Owned runner only.
const crypto=require('node:crypto'),fs=require('node:fs/promises'),path=require('node:path');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
const {managedEdge}=require('../auth/managed-edge.cjs');
const {web,sleep,createMember,memberToken,rootAdminToken,cli,sql,call,claims,matchReady}=require('../auth/local-tokens.cjs');
if(!process.env.SWOOVR_TEST_API_BASE||!process.env.SWOOVR_TEST_APP_DATABASE)throw Error('Use the owned regression runner.');
let checks=0,phase='setup';const check=(ok,label)=>{if(!ok){const error=Error(label);error.safeLabel=true;throw error;}checks++;};
const title='There is an update to a report you submitted. Open Notices and reports to review it.';
(async()=>{
 const edge=await managedEdge(chromium),browser=edge.browser,contexts=[];
 const folder=path.resolve(process.env.SWOOVR_SCREENSHOTS||'.local/suite-shots','2026-10-30-report-outcomes');await fs.mkdir(folder,{recursive:true});
 try{
  const root=await rootAdminToken(browser),members={},tokens={},ids={},pids={};
  for(const name of ['reporter','subject','other'])members[name]=await createMember('outcome-'+name);
  await cli('beta','add',...Object.values(members).map(x=>x.email));
  for(const name of Object.keys(members)){
   tokens[name]=await memberToken(browser,members[name]);const profile=await call(tokens[name],'/api/me/profile',{method:'PUT',body:{displayName:'Fictional outcome '+name,homeCityId:2078025,about:'Fictional profile for report testing'}});
   check(profile.status===200,'fictional profile accepted');pids[name]=profile.body.publicId;ids[name]=await sql(`SELECT id FROM accounts WHERE subject='${claims(tokens[name]).sub}'`);
   check((await call(tokens[name],'/api/me/modes/friends',{method:'PUT',body:{enabled:true,visible:true,shareCoreAbout:true}})).status===200,'mode accepted');check((await matchReady(tokens[name])).every(s=>s===200),'adult matching ready');
  }
  phase='routine-web-report';
  const context=await browser.newContext({viewport:{width:390,height:844}});contexts.push(context);const page=await context.newPage();
  await page.goto(web+'/account/sign-in?intro=done');await page.fill('#username',members.reporter.email);await page.fill('#password',members.reporter.password);
  await Promise.all([page.waitForURL(web+'/account'),page.click('#kc-login')]);await page.goto(web+'/people/'+pids.subject);
  await page.click('[data-report-open]');await page.selectOption('#report-category','harassment');await page.fill('#report-text','Fictional private report note');await page.click('[data-report-form] button[type=submit]');await page.waitForSelector('[data-report="sent"]');
  check(await page.locator('[data-report-support]').count()===1,'routine report always offers support and complaints');
  const support=page.locator('[data-report-support]');check((await support.innerText()).includes('how your report was handled'),'handling complaint explained');check((await support.innerText()).includes('Online Dating Code'),'Code concerns explained without claiming signatory status');
  check(await support.getByRole('link',{name:'Safety & Support',exact:true}).getAttribute('href')==='/safety','country-aware independent support link');
  check(await support.getByRole('link',{name:'Contact Support',exact:true}).getAttribute('href')==='https://swoovr.com/support','approved corporate support channel');
  check(await support.getByRole('link',{name:'Email Support',exact:true}).getAttribute('href')==='mailto:support@swoovr.com','approved support email');
  check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'support fits390px');await page.screenshot({path:path.join(folder,'web-report-support-390.png'),fullPage:true});
  const reportId=await sql(`SELECT id FROM reports WHERE reporter_account_id='${ids.reporter}' AND subject_account_id='${ids.subject}'`),caseId=await sql(`SELECT case_id FROM reports WHERE id='${reportId}'`);
  check(await sql(`SELECT revision FROM report_outcome_states WHERE report_id='${reportId}'`)==='0','new report and silent baseline commit together');
  const outcomes=async token=>(await call(token,'/api/me/notifications')).body.items.filter(n=>n.kind==='reportOutcome');
  check((await outcomes(tokens.reporter)).length===0,'submission sends no historical outcome spam');
  phase='human-action-transactions';
  const action=await call(root,`/api/admin/cases/${caseId}/actions`,{method:'POST',body:{action:'warn',reason:'Fictional private staff explanation'}});check(action.status===200,'actual human warning succeeds');
  const sanction=action.body.sanctions[0].id;check((await outcomes(tokens.reporter)).length===1,'real case action creates one reporter update');
  check((await call(root,`/api/admin/sanctions/${sanction}/lift`,{method:'POST',body:{reason:'Fictional reviewed lift'}})).status===200,'actual lift succeeds');
  check((await outcomes(tokens.reporter)).length===2,'lift creates a new metadata revision even before closure');
  check((await call(root,`/api/admin/cases/${caseId}/close`,{method:'POST'})).status===200,'actual case closes');
  let notices=await outcomes(tokens.reporter);check(notices.length===3,'closure creates a distinct reporter update');
  check(notices.every(n=>n.title===title&&n.link==='/me/notices'),'centre wording and links are generic');
  check(!JSON.stringify(notices).includes('private')&&!JSON.stringify(notices).includes('subject'),'no private reason/source identity in centre');
  check((await outcomes(tokens.subject)).length===0&&(await outcomes(tokens.other)).length===0,'only reporting member receives outcome rows');
  const first=notices[0];check((await call(tokens.other,`/api/me/notifications/${first.id}/read`,{method:'POST'})).status===404,'another member cannot read the notification');
  await call(root,`/api/admin/cases/${caseId}`);await call(root,`/api/admin/cases/${caseId}`);
  check((await call(root,`/api/admin/cases/${caseId}/close`,{method:'POST'})).status===409,'repeated close is refused');
  check((await outcomes(tokens.reporter)).length===3,'case views/replayed close do not manufacture updates');
  check(await sql(`SELECT count(*) FROM notification_deliveries d JOIN notifications n ON n.id=d.notification_id WHERE n.kind='ReportOutcome' AND n.account_id='${ids.reporter}' AND d.channel IN ('push','email')`)==='0','centre-only default creates no external dispatch');
  await page.goto(web+'/me/notifications');await page.waitForSelector('[data-notifications="ready"]');
  check((await page.locator('body').innerText()).includes(title),'actual web centre renders generic update');await page.screenshot({path:path.join(folder,'web-report-outcomes-390.png'),fullPage:true});
  await page.goto(web+'/me/notices');await page.waitForSelector('[data-notices="ready"]');check(await page.locator('[data-report-support]').count()===1,'Notices offers complaint and safety paths');
  check((await call(tokens.reporter,'/api/me/reports')).body.find(r=>r.id===reportId).outcome==='actionTaken','reporter sees approved general outcome only');
  phase='atomic-baseline-failure';
  const guard='ck_owned_outcome_'+crypto.randomBytes(8).toString('hex');
  await sql(`ALTER TABLE report_outcome_states ADD CONSTRAINT ${guard} CHECK(source_account_id<>'${ids.other}'::uuid)`);
  try{
   const rejected=await call(tokens.other,'/api/reports',{method:'POST',body:{subjectType:'profile',subjectId:pids.subject,category:'harassment',text:'Fictional atomic failure fixture',block:true}});
   check(rejected.status===500,'injected checkpoint failure refuses the whole operation');
   check(await sql(`SELECT count(*) FROM reports WHERE reporter_account_id='${ids.other}'`)==='0','failed baseline leaves no report');
   check(await sql(`SELECT count(*) FROM blocks WHERE blocker_account_id='${ids.other}' AND blocked_account_id='${ids.subject}'`)==='0','failed baseline leaves no optional block');
  }finally{await sql(`ALTER TABLE report_outcome_states DROP CONSTRAINT ${guard}`);}
  const otherContext=await browser.newContext({viewport:{width:390,height:844}});contexts.push(otherContext);const otherPage=await otherContext.newPage();
  await otherPage.goto(web+'/account/sign-in?intro=done');await otherPage.fill('#username',members.other.email);await otherPage.fill('#password',members.other.password);await Promise.all([otherPage.waitForURL(web+'/account'),otherPage.click('#kc-login')]);
  await otherPage.goto(web+'/people/'+pids.subject);await otherPage.click('[data-report-open]');await otherPage.selectOption('#report-category','harassment');await otherPage.locator('[data-report-form] input[type=checkbox]').check();await otherPage.click('[data-report-form] button[type=submit]');await otherPage.waitForSelector('[data-person="blocked"]');
  check(await otherPage.locator('[data-report-support]').count()===1,'report-and-block retains support after source profile disappears');
  check(await sql(`SELECT count(*) FROM reports WHERE reporter_account_id='${ids.other}'`)==='1','normal retry creates source and baseline');
  await otherPage.screenshot({path:path.join(folder,'web-report-block-support-390.png'),fullPage:true});
  check((await outcomes(tokens.other)).length===0,'retry starts silent without a false outcome');
  phase='restricted-reporter';
  check((await call(root,`/api/admin/accounts/${ids.reporter}/sanctions`,{method:'POST',body:{action:'suspend',days:1,reason:'Fictional restriction acceptance fixture'}})).status===200,'reporter restriction fixture applied');
  check((await outcomes(tokens.reporter)).length===3,'barred reporter retains own safe outcome centre');check((await call(tokens.reporter,'/api/me/reports')).status===200,'barred reporter can read own general report outcomes');
  console.log(`PASS: report outcomes and complaint guidance (${checks} checks; owned real API/PostgreSQL/Web transactions, fictional data, centre-only).`);
 }finally{for(const context of contexts)await context.close();await edge.close();}
})().catch(error=>{console.error('FAIL outcome phase '+phase+': '+(error.safeLabel?error.message:'inspection required; raw payload withheld'));process.exitCode=1;});
