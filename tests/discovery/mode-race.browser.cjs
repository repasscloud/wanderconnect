// Real eligible mode-specific API projections, delayed in Edge to reproduce stale-response cross-mode mixing.
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');const {managedEdge}=require('../auth/managed-edge.cjs');
const {web,createMember,memberToken,cli,call,matchReady}=require('../auth/local-tokens.cjs');
if(!process.env.SWOOVR_TEST_APP_DATABASE)throw Error('Use owned runner.');
let checks=0;const check=(ok,label)=>{if(!ok)throw Error(label);checks++;};
(async()=>{const edge=await managedEdge(chromium);let context,release;
try{
 const members={},tokens={};for(const name of ['viewer','peer']){members[name]=await createMember('mode-race-'+name);await cli('beta','add',members[name].email);tokens[name]=await memberToken(edge.browser,members[name]);
  check((await call(tokens[name],'/api/me/profile',{method:'PUT',body:{displayName:'Fictional '+name,homeCityId:2078025}})).status===200,'fictional profile');check((await matchReady(tokens[name])).every(s=>s===200),'adult matching readiness');
  for(const mode of ['friends','business'])check((await call(tokens[name],'/api/me/modes/'+mode,{method:'PUT',body:{enabled:true,visible:true,shareCoreAbout:false,about:'FICTIONAL '+mode.toUpperCase()+' ONLY'}})).status===200,'mode-specific profile');
 }
 context=await edge.browser.newContext();const page=await context.newPage();await page.goto(web+'/account/sign-in?intro=done');await page.fill('#username',members.viewer.email);await page.fill('#password',members.viewer.password);await Promise.all([page.waitForURL(web+'/account'),page.click('#kc-login')]);
 let delay=false,intercepted=0,start;const started=new Promise(r=>start=r),blocked=new Promise(r=>release=r);
 await page.route('**/bff/api/explore?**',async route=>{const mode=new URL(route.request().url()).searchParams.get('mode');if(!delay||mode!=='business')return route.continue();intercepted++;const response=await route.fetch();check(response.status()===200,'real business response eligible');start();await blocked;await route.fulfill({response});});
 await page.goto(web+'/explore');await page.waitForSelector('[data-explore-state=ready]');check((await page.locator('.people-grid').innerText()).includes('FICTIONAL FRIENDS ONLY'),'initial requested mode projection');
 delay=true;await page.click('[data-mode=business]');await started;await page.click('[data-mode=friends]');await page.waitForSelector('[data-explore-mode=friends][data-explore-state=ready]');
 check((await page.locator('.people-grid').innerText()).includes('FICTIONAL FRIENDS ONLY'),'newer friends response completes first');
 const observed=page.waitForResponse(r=>r.url().includes('/bff/api/explore?')&&new URL(r.url()).searchParams.get('mode')==='business');release();await observed;await page.waitForTimeout(500);
 check(intercepted===1,'older response actually delayed');check(!(await page.locator('.people-grid').innerText()).includes('FICTIONAL BUSINESS ONLY'),'older business response cannot overwrite friends projection');
 console.log(`PASS: discovery mode response ordering (${checks} checks; actual eligible API projections, controlled transport delay, fictional adults).`);
}finally{release?.();if(context)await context.close();await edge.close();}})().catch(e=>{console.error(e.message);process.exitCode=1;});
