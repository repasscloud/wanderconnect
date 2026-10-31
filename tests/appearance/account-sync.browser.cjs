// Real local Keycloak/BFF/PostgreSQL and rendered WASM controls. Fictional adults only.
const assert=require('node:assert/strict'),crypto=require('node:crypto'),fs=require('node:fs/promises'),path=require('node:path');
if(!process.env.SWOOVR_TEST_APP_DATABASE)throw Error('Use the owned runner.');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright'),{managedEdge}=require('../auth/managed-edge.cjs');
const {web,call,createMember,memberToken,rootAdminToken,cli,claims}=require('../auth/local-tokens.cjs');
(async()=>{const edge=await managedEdge(chromium);let checks=0;
try{
 await rootAdminToken(edge.browser);const a=await createMember('appearance-ui');await cli('beta','add',a.email);const token=await memberToken(edge.browser,a);await call(token,'/api/me');
 const owner=crypto.createHash('sha256').update(claims(token).iss+'\0'+claims(token).sub).digest('hex');
 const context=await edge.browser.newContext({viewport:{width:390,height:900},colorScheme:'light'}),page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.stack||e.message));
 await page.goto(web+'/settings/appearance');await page.locator('input[name=scheme][value=coral]').check();
 await page.goto(web+'/account/sign-in');await page.fill('#username',a.email);await page.fill('#password',a.password);await page.click('#kc-login');await page.waitForURL(web+'/account');
 await page.goto(web+'/settings/appearance');await page.locator('[data-account-appearance=ready]').waitFor();
 const state=async(s,m)=>{await page.waitForFunction(([s,m])=>document.documentElement.dataset.scheme===s&&document.documentElement.dataset.requestedMode===m,[s,m]);assert.equal(await page.locator(`input[name=scheme][value=${s}]`).isChecked(),true);checks++;};
 const saved=async(s,m)=>{await page.waitForFunction(async([s,m])=>{let r=await fetch('/bff/api/me/appearance',{headers:{'X-Swoovr-Bff':'1'}});if(!r.ok)return false;let v=await r.json();return v.preference.scheme===s&&v.preference.mode===m;},[s,m]);checks++;};
 await state('coral','system');await saved('coral','system');
 for(const scheme of require('../../src/Swoovr.Web/wwwroot/appearance-palettes.js').map(p=>p.id)){
  // IDs come from the generated canonical catalog; do not infer display labels.
  const input=page.locator(`input[name=scheme][value=${scheme}]`);
  await input.check();for(const mode of ['light','dark','system']){await page.locator(`input[name=mode][value=${mode}]`).check();await state(scheme,mode);await saved(scheme,mode);}
 }
 let offline=true;await page.route('**/bff/api/me/appearance',route=>offline&&route.request().method()==='PUT'?route.abort('internetdisconnected'):route.continue());
 await page.locator('input[name=scheme][value=pride]').check();await state('pride','system');
 await page.getByRole('status').filter({hasText:/offline|retry|could not|sync/i}).first().waitFor();
 const pending=await page.evaluate(()=>JSON.parse(JSON.parse(localStorage.getItem('swoovr.appearance.account.v1')).json).pending);assert.ok(pending?.changeId);checks++;
 await page.reload();await page.locator('[data-account-appearance=ready]').waitFor();await state('pride','system');
 offline=false;await page.getByRole('button',{name:'Retry sync',exact:true}).click();await saved('pride','system');
 const remote=await call(token,'/api/me/appearance');await call(token,'/api/me/appearance',{method:'PUT',headers:{'Swoovr-Appearance-Owner':owner},body:{preference:{scheme:'coral',mode:'dark'},expectedRevision:remote.body.revision,changeId:crypto.randomUUID()}});
 await page.locator('input[name=scheme][value=union]').check();await page.locator('[data-appearance-conflict]').waitFor();await state('union','system');
 await page.getByRole('button',{name:'Use the account choice',exact:true}).click();await state('coral','dark');
 await page.getByRole('button',{name:'Reset appearance',exact:true}).click();await state('standard','system');await saved('standard','system');
 const html=await page.request.get(web+'/settings/appearance');assert.match(html.headers()['cache-control'],/no-store/);assert.ok(!(await html.text()).includes(token));checks++;
 if(process.env.SWOOVR_SCREENSHOTS){await fs.mkdir(process.env.SWOOVR_SCREENSHOTS,{recursive:true});await page.screenshot({path:path.join(process.env.SWOOVR_SCREENSHOTS,'account-appearance-390.png'),fullPage:true});}

 let releaseLoad,loadObserved;const observed=new Promise(resolve=>loadObserved=resolve);const waiting=new Promise(resolve=>releaseLoad=resolve);
 await page.unroute('**/bff/api/me/appearance');await page.route('**/bff/api/me/appearance',async route=>{if(route.request().method()==='GET'){loadObserved();await waiting;}await route.continue();});
 await page.reload({waitUntil:'domcontentloaded'});await observed;assert.equal(await page.evaluate(()=>document.documentElement.dataset.scheme),'standard','cached account choice applies before its HTTP response');checks++;releaseLoad();await page.locator('[data-account-appearance=ready]').waitFor();await page.unroute('**/bff/api/me/appearance');
 const second=await context.newPage();await second.goto(web+'/settings/appearance');await second.locator('[data-account-appearance=ready]').waitFor();await second.locator('input[name=scheme][value=eucalyptus]').check();
 await page.waitForFunction(()=>document.documentElement.dataset.scheme==='eucalyptus');checks++;await second.close();
 await page.goto(web+'/account');await page.getByRole('button',{name:'Sign out',exact:true}).click();await page.waitForURL(url=>!url.pathname.includes('/realms/'));
 await page.goto(web+'/settings/appearance');await page.locator('input[name=scheme][value=coral]').waitFor();
 assert.equal(await page.evaluate(()=>localStorage.getItem('swoovr.appearance.account.v1')),null);assert.equal(await page.evaluate(()=>document.documentElement.dataset.scheme),'coral');checks++;
 const b=await createMember('appearance-ui-switch');await cli('beta','add',b.email);const bt=await memberToken(edge.browser,b);await call(bt,'/api/me');
 await page.goto(web+'/account/sign-in');await page.fill('#username',b.email);await page.fill('#password',b.password);await page.click('#kc-login');await page.waitForURL(web+'/account');await page.goto(web+'/settings/appearance');await page.locator('[data-account-appearance=ready]').waitFor();await state('coral','system');
 assert.equal((await call(bt,'/api/me/appearance')).body.preference.scheme,'coral');checks++;
 await context.addInitScript(()=>Object.defineProperty(window,'localStorage',{get(){throw Error('injected storage denial');}}));await page.reload();await page.locator('[data-account-appearance=ready]').waitFor();await page.locator('input[name=scheme][value=union]').check();await state('union','system');await saved('union','system');assert.match(await page.locator('.save-area').innerText(),/storage is unavailable/);checks++;
 assert.ok(errors.every(e=>e.includes('injected storage denial')&&e.includes('/_framework/blazor.web.')&&e.includes('startLoadingWebAssemblyIfNotStarted')),JSON.stringify(errors));checks++;await context.close();console.log(`PASS account appearance UI: ${checks} checks; real local auth/BFF/store, guest seed, schemes/modes, offline pending/reload/retry, explicit conflict, reset, private HTML.`);
}finally{await edge.close();}})().catch(e=>{console.error(e);process.exitCode=1});
