// Actual local OIDC via a rendered link under strict CSP; no real identity/provider delivery.
const assert=require('node:assert/strict'),crypto=require('node:crypto');if(!process.env.SWOOVR_TEST_APP_DATABASE)throw Error('Use owned runner.');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright'),{managedEdge}=require('../auth/managed-edge.cjs');
const {web,createMember,cli}=require('../auth/local-tokens.cjs');
(async()=>{const edge=await managedEdge(chromium);let context,checks=0;try{
 const response=await edge.browser.newContext();
 try{
  const r=await response.request.get(web+'/account');const html=await r.text();const map=html.match(/<script\b[^>]*type="importmap"[^>]*>([\s\S]*?)<\/script>/i);assert.ok(map,'actual SSR must contain its fingerprinted import map');checks++;
  const integrity='sha256-'+crypto.createHash('sha256').update(map[1].replace(/\r\n?/g,'\n')).digest('base64');const scriptPolicy=(r.headers()['content-security-policy']??'').split(';').find(v=>v.trim().startsWith('script-src '));
  assert.ok(scriptPolicy?.includes("'"+integrity+"'"),'CSP must authorize the exact rendered import map, not only same-origin scripts');checks++;
  assert.ok(!scriptPolicy.includes("'unsafe-inline'")&&!scriptPolicy.includes("'unsafe-eval'"),'do not weaken executable script policy');checks++;
  const probe=await response.newPage();await probe.addInitScript(()=>{window.__swoovrScriptViolations=[];document.addEventListener('securitypolicyviolation',e=>{if(e.effectiveDirective.startsWith('script-src'))window.__swoovrScriptViolations.push(e.effectiveDirective);});});
  await probe.goto(web+'/account');
  assert.equal(await probe.locator('script[type="importmap"]').getAttribute('integrity'),integrity,'browser-decoded map attribute and HTTP policy must share integrity');checks++;
  const imports=JSON.parse(map[1]).imports;const specifier=Object.keys(imports).find(k=>k.endsWith('/appearance-palettes.js'));assert.ok(specifier,'exercise a real fingerprinted module mapping');checks++;
  const expected=new URL(imports[specifier],web+'/').href;const modules=[];probe.on('request',r=>modules.push(r.url()));
  assert.equal(await probe.evaluate(async key=>{await import(key);return true;},specifier),true);checks++;
  assert.ok(modules.includes(expected),'browser must resolve the map to the fingerprinted URL');checks++;
  assert.deepEqual(await probe.evaluate(()=>window.__swoovrScriptViolations),[],'legitimate import map/module must not violate CSP');checks++;
  await probe.evaluate(()=>window.__swoovrEnhancedDocument=true);await probe.locator('a[href="/privacy"]').first().click();await probe.waitForURL(web+'/privacy');
  assert.equal(await probe.evaluate(()=>window.__swoovrEnhancedDocument),true,'ordinary enhanced navigation remains enabled');checks++;
  assert.deepEqual(await probe.evaluate(()=>window.__swoovrScriptViolations),[],'enhanced navigation must retain a valid import map policy');checks++;
  await probe.evaluate(()=>{window.__swoovrUnexpectedInline=false;const script=document.createElement('script');script.textContent='window.__swoovrUnexpectedInline=true';document.head.append(script);});
  await probe.waitForFunction(()=>window.__swoovrScriptViolations.length>0);
  assert.equal(await probe.evaluate(()=>window.__swoovrUnexpectedInline),false,'unrelated inline executable script remains blocked');checks++;
  assert.ok((await probe.evaluate(()=>window.__swoovrScriptViolations)).includes('script-src-elem'));checks++;
 }finally{await response.close();}
 const member=await createMember('hosting-auth-navigation');await cli('beta','add',member.email);context=await edge.browser.newContext();const page=await context.newPage();let fetchToIdentity=false,blockedConnect=false;
 page.on('request',r=>{if(r.resourceType()==='fetch'&&new URL(r.url()).port==='8180')fetchToIdentity=true;});
 page.on('console',m=>{if(m.type()==='error'&&m.text().includes('connect-src'))blockedConnect=true;});
 await page.goto(web+'/account');const link=page.locator('a[href="/account/sign-in"]').first();assert.equal(await link.getAttribute('data-enhance-nav'),'false');checks++;
 await link.click();await page.waitForSelector('#username');await page.fill('#username',member.email);await page.fill('#password',member.password);await page.click('#kc-login');await page.waitForURL(url=>url.href.startsWith(web+'/account')||url.search.includes('TERMS_AND_CONDITIONS'));if(page.url().includes('TERMS_AND_CONDITIONS'))await page.click('[name=accept]');await page.waitForURL(web+'/account');
 const session=await page.request.get(web+'/bff/api/session',{headers:{'X-Swoovr-Bff':'1'}});assert.equal(session.status(),200);checks++;
 await page.waitForSelector('[data-account-panel=ready]');assert.equal(await page.locator('[data-account-panel=ready]').count(),1);checks++;
 assert.equal(fetchToIdentity,false,'identity handoff must be a document navigation');checks++;assert.equal(blockedConnect,false,'identity hop must not need broader connect-src');checks++;
 if(process.env.SWOOVR_SCREENSHOTS){await page.goto(web+'/account/data');await page.waitForSelector('[data-data-panel=none]');await page.setViewportSize({width:390,height:900});await require('node:fs/promises').mkdir(process.env.SWOOVR_SCREENSHOTS,{recursive:true});await page.screenshot({path:require('node:path').join(process.env.SWOOVR_SCREENSHOTS,'data-390.png'),fullPage:true});}
 console.log('PASS import-map CSP/rendered authentication: '+checks+' checks; exact hash/SRI, fingerprinted module resolution, enhanced navigation, unrelated inline refusal and actual local OIDC/WASM. No hosted/provider acceptance.');
}finally{if(context)await context.close();await edge.close();}})().catch(e=>{console.error(e);process.exitCode=1});
