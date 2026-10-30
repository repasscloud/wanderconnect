// Actual owned API/PostgreSQL/Admin. Historical dates below are explicitly synthetic; all actors fictional.
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
const {managedEdge}=require('../auth/managed-edge.cjs');
const {api,web,admin,createMember,createStaff,memberToken,staffToken,rootAdminToken,cli,call,claims,matchReady,sql}=require('../auth/local-tokens.cjs');
if(!process.env.SWOOVR_TEST_APP_DATABASE)throw Error('Use owned runner.');
(async()=>{const edge=await managedEdge(chromium),contexts=[];let checks=0;const check=(ok,label)=>{assert.ok(ok,label);checks++;};
try{
 const root=await rootAdminToken(edge.browser);let result=await call(root,'/api/admin/beta-scorecard');check(result.status===200,'current admin reads scorecard: '+result.status);
 check(result.body.dataset==='local-synthetic'&&result.body.coverage==='retained-source-subset-not-population-totals','explicit synthetic source scope');
 const members={},tokens={},ids={};for(const label of ['viewer','peer']){
  members[label]=await createMember('beta-scorecard-'+label);await cli('beta','add',members[label].email);tokens[label]=await memberToken(edge.browser,members[label]);
  check((await call(tokens[label],'/api/me/profile',{method:'PUT',body:{displayName:'Fictional Beta '+label,homeCityId:2078025}})).status===200,'actual profile setup');await matchReady(tokens[label]);
  check((await call(tokens[label],'/api/me/modes/friends',{method:'PUT',body:{enabled:true,visible:true}})).status===200,'actual member mode setup');
  ids[label]=await sql(`SELECT id FROM accounts WHERE subject='${claims(tokens[label]).sub}'`);
 }
 const explored=await call(tokens.viewer,'/api/explore?mode=friends');check(explored.status===200&&explored.body.people.length>0,'actual eligible discovery');
 await call(tokens.viewer,'/api/explore?mode=friends');
 check(await sql(`SELECT count(*) FROM beta_journey_days WHERE account_id='${ids.viewer}' AND day=(now() AT TIME ZONE 'UTC')::date`)==='1','actual API activity daily deduplication');
 check(await sql(`SELECT explore_succeeded AND explore_has_peer FROM beta_journey_days WHERE account_id='${ids.viewer}' AND day=(now() AT TIME ZONE 'UTC')::date`)==='t','actual successful Explore observations merged');
 check((await call(tokens.viewer,'/api/admin/beta-scorecard')).status===403,'member cannot read aggregate staff route');
 check((await call(null,'/api/admin/beta-scorecard')).status===401,'anonymous cannot read scorecard');
 for(const query of ['weeksAgo=0','weeksAgo=13','weeksAgo=1&weeksAgo=2','mode=friends','accountId='+ids.viewer])check((await call(root,'/api/admin/beta-scorecard?'+query)).status===400,'unsupported week/filter rejected');
 const staff={};for(const role of ['admin','support']){staff[role]=await createStaff('beta-scorecard-'+role);check((await call(root,'/api/admin/invitations',{method:'POST',body:{email:staff[role].email,role}})).status===201,'fictional staff invitation');const token=await staffToken(edge.browser,staff[role]);check((await call(token,'/api/admin/beta-scorecard')).status===(role==='admin'?200:403),'current staff role boundary');}
 const response=await fetch(api+'/api/admin/beta-scorecard',{headers:{Authorization:'Bearer '+root,'Swoovr-Version':'2026-10-28'}});check(response.headers.get('cache-control')==='no-store','API aggregate not cached');
 const safe=JSON.stringify(await response.json());check(!safe.includes(members.viewer.email)&&!safe.includes(ids.viewer)&&!safe.includes('"accountId"')&&!safe.includes('"body"')&&!safe.includes('"filters"'),'no member identifiers or raw private source in scorecard');
 const from=result.body.fromInclusive;
 await sql(`UPDATE accounts SET created_at='${from}'::timestamptz+interval '1 day' WHERE id='${ids.viewer}'; INSERT INTO beta_journey_days(account_id,day,dataset,explore_succeeded,explore_has_peer) VALUES('${ids.viewer}','${from}'::date+2,'local-synthetic',true,true)`);
 result=await call(root,'/api/admin/beta-scorecard');check(result.body.metrics.filter(m=>m.status!=='not-recorded').every(m=>m.status==='suppressed-small-cells'&&m.numerator===null&&m.denominator===null&&m.percentage===null),'synthetic historical small cohort suppresses every numeric cell');
 check(result.body.metrics.find(m=>m.key==='real-world-outcomes').status==='not-recorded','meeting closure not claimed as real-world success');
 const context=await edge.browser.newContext({viewport:{width:390,height:844}});contexts.push(context);const page=await context.newPage();
 await page.goto(admin+'/sign-in');await page.fill('#username',staff.admin.email);await page.fill('#password',staff.admin.password);await Promise.all([page.waitForURL(admin+'/'),page.click('#kc-login')]);
 const document=await page.goto(admin+'/beta-scorecard');await page.waitForSelector('[data-beta-scorecard]');check((document.headers()['cache-control']||'').split(',').some(value=>value.trim()==='no-store'),'staff SSR contains no-store directive: '+document.headers()['cache-control']);
 check(await page.locator('[data-beta-status=suppressed-small-cells]').count()>0,'Admin renders suppressed status explicitly');
 check((await page.locator('[data-beta-scorecard]').innerText()).includes('not zero'),'unavailable/suppressed not described as zero');
 for(const width of [390,320]){await page.setViewportSize({width,height:844});check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'staff scorecard fits narrow width');}
 const folder=path.resolve(process.env.SWOOVR_SCREENSHOTS||'.local/suite-shots');await fs.mkdir(folder,{recursive:true});await page.locator('h1').evaluate(e=>e.scrollIntoView());await page.screenshot({path:path.join(folder,'admin-beta-scorecard-320.png'),fullPage:false});
 console.log(`PASS: beta scorecard (${checks} checks; actual owned API/activity/Admin, synthetic historical suppression, no analytics provider or attendance claim).`);
}finally{for(const context of contexts)await context.close();await edge.close();}})().catch(e=>{console.error(e.message);process.exitCode=1;});
