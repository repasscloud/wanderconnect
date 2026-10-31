// Real owned API/Web/CLI with fictional adults. Draft reading is not acceptance; no legal source is activated.
const fs=require('node:fs/promises'),path=require('node:path'),crypto=require('node:crypto');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
const {managedEdge}=require('../auth/managed-edge.cjs');
const {web,createMember,memberToken,rootAdminToken,cli,sql,call,claims,matchReady}=require('../auth/local-tokens.cjs');
if(!process.env.SWOOVR_TEST_APP_DATABASE||process.env.PolicyNotices__Enabled!=='true')throw Error('Use the owned runner with PolicyNotices__Enabled=true.');
let checks=0;const check=(ok,label)=>{if(!ok)throw Error(label);checks++;};
(async()=>{const edge=await managedEdge(chromium),browser=edge.browser,contexts=[];
try{
 const folder=path.resolve(process.env.SWOOVR_SCREENSHOTS||'.local/suite-shots','2026-10-31-public-policies');await fs.mkdir(folder,{recursive:true});
 const root=await rootAdminToken(browser);await call(root,'/api/session');const operator=await sql(`SELECT id FROM accounts WHERE realm='Staff' AND subject='${claims(root).sub}'`);
 const context=await browser.newContext({viewport:{width:390,height:844}});contexts.push(context);const page=await context.newPage();
 for(const [route,id]of [['/terms','terms'],['/community-guidelines','community']]){
  await page.goto(web+route);await page.waitForSelector('[data-public-policy]');check(await page.locator('[data-public-policy]').getAttribute('data-policy-status')==='draft','public policy is explicitly draft');
  check((await page.locator('body').innerText()).includes('Not in force')&&(await page.locator('body').innerText()).includes('does not accept'),'draft reader records no consent claim');
  check(await page.locator('input[type=checkbox]').count()===0,'draft reader has no acceptance checkbox');
  for(const width of [390,320]){await page.setViewportSize({width,height:844});check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'draft reader fits narrow viewport');}
  await page.setViewportSize({width:390,height:844});await page.screenshot({path:path.join(folder,id+'-draft-390.png'),fullPage:true});
 }
 await page.goto(web+'/safety/advice');check((await page.locator('body').innerText()).includes('Responsibility for harmful behaviour lies with the person who causes it'),'advice avoids blaming the person harmed');
 check((await page.locator('body').innerText()).includes('does not monitor emergencies'),'advice makes emergency limits explicit');
 await page.goto(web+'/safety');check(await page.locator('a[href="/terms"]').count()>0&&await page.locator('a[href="/community-guidelines"]').count()>0,'public support links both policies');
 await page.goto(web+'/policies/fictional-unknown');check(await page.locator('[data-approved-policy-unavailable]').count()===1,'no fake approved source is rendered');
 const members={},tokens={};for(const name of ['first','second']){members[name]=await createMember('policy-'+name);await cli('beta','add',members[name].email);tokens[name]=await memberToken(browser,members[name]);check((await call(tokens[name],'/api/me')).status===200,'fictional member initialized');}
 check((await call(tokens.first,'/api/me/policies')).body.requiredVersion===null,'real API has no approved legal requirement');
 check((await call(tokens.first,'/api/me/policies/accept',{method:'POST',body:{version:'draft-2026-10-30-0.1',confirm:true}})).status===400,'draft acceptance refused by real API');
 check(await sql('SELECT count(*) FROM policy_acceptances')==='0','no implicit or draft acceptance rows');
 await page.goto(web+'/account/sign-in?intro=done');await page.fill('#username',members.first.email);await page.fill('#password',members.first.password);await Promise.all([page.waitForURL(web+'/account'),page.click('#kc-login')]);await page.waitForSelector('[data-account-panel=ready]');
 check(await page.locator('[data-safety-advice]').count()===1,'first-use Me prominently shows advice before profile setup');check(await page.locator('a[href="/terms"]').count()>0&&await page.locator('a[href="/community-guidelines"]').count()>0,'Me links draft policies');
 await page.screenshot({path:path.join(folder,'first-use-advice-390.png'),fullPage:true});
 await page.goto(web+'/account/policies');await page.waitForSelector('[data-policy-none]');check(await page.locator('input[type=checkbox]').count()===0,'no checkbox when no approved requirement exists');
 check((await matchReady(tokens.first)).every(s=>s===200),'fictional adult setup accepted');await call(tokens.first,'/api/me/profile',{method:'PUT',body:{displayName:'Fictional policy tester',homeCityId:2078025}});
 await page.goto(web+'/me/profile');await page.waitForSelector('[data-safety-advice]');check(await page.locator('[data-safety-advice]').count()===1,'profile retains educational advice');
 await page.goto(web+'/me/meeting-safety');await page.waitForSelector('[data-meeting-plan-form]');check(await page.locator('[data-meeting-loading]').count()===0,'meeting advice remains after the real plan form loads');check((await page.locator('body').innerText()).includes('Choose a public place'),'meeting setup provides relevant advice');await page.screenshot({path:path.join(folder,'before-meeting-advice-390.png'),fullPage:true});
 const version='fixture-'+crypto.randomBytes(8).toString('hex');const staged=await cli('policies','notices','stage',version,'Fictional test safety update','/safety/advice','--operator',operator);check(staged.includes('Nothing delivered'),'stage has no delivery effect');
 const id=await sql(`SELECT id FROM policy_notice_publications WHERE version='${version}'`);
 await cli('policies','notices','publish',id,'--operator',operator,'--source-published','true');check(await sql(`SELECT count(*) FROM policy_notice_recipients WHERE publication_id='${id}'`)==='2','explicit CLI publication snapshots current members');
 const late=await createMember('policy-late');await cli('beta','add',late.email);const lateToken=await memberToken(browser,late);await call(lateToken,'/api/me');
 await cli('policies','notices','publish',id,'--operator',operator,'--source-published','true');await cli('policies','notices','process',id);await cli('policies','notices','process',id);
 const centre=(await call(tokens.first,'/api/me/notifications')).body.items.filter(n=>n.kind==='policyChange');
 check(centre.length===1&&centre[0].link==='/safety/advice'&&centre[0].title.includes('Fictional test safety update'),'real API projects one safe centre item');
 check((await call(lateToken,'/api/me/notifications')).body.items.every(n=>n.kind!=='policyChange'),'late member gets no historical broadcast');
 check(await sql(`SELECT count(*) FROM notification_deliveries d JOIN notifications n ON n.id=d.notification_id WHERE n.kind='PolicyChange'`)==='0','centre publication never queues email or push');
 const foreign=(await call(tokens.second,'/api/me/notifications')).body.items.find(n=>n.kind==='policyChange');check(foreign&&foreign.id!==centre[0].id,'each member has an independent own notice');
 check((await call(tokens.second,`/api/me/notifications/${centre[0].id}/read`,{method:'POST'})).status===404,'other member cannot mark source notice read');
 await page.goto(web+'/me/notifications');await page.waitForSelector('[data-notifications=ready]');check((await page.locator('body').innerText()).includes('Fictional test safety update'),'actual Web centre renders public source title');check(await page.locator('a[href="/safety/advice"]').count()>0,'Web policy notice links advice');await page.screenshot({path:path.join(folder,'policy-notice-centre-390.png'),fullPage:true});
 check(await sql('SELECT count(*) FROM policy_acceptances')==='0','reading advice and policy notices never accepts a draft');
 console.log(`PASS: public drafts safety advice and policy notices (${checks} checks; owned real API/Web/CLI, fictional data, no approved legal activation/external delivery).`);
}finally{for(const context of contexts)await context.close();await edge.close();}})().catch(e=>{console.error(e.message);process.exitCode=1;});
