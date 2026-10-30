// Actual local OIDC via a rendered link under strict CSP; no real identity/provider delivery.
const assert=require('node:assert/strict');if(!process.env.SWOOVR_TEST_APP_DATABASE)throw Error('Use owned runner.');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright'),{managedEdge}=require('../auth/managed-edge.cjs');
const {web,createMember,cli}=require('../auth/local-tokens.cjs');
(async()=>{const edge=await managedEdge(chromium);let context;try{
 const member=await createMember('hosting-auth-navigation');await cli('beta','add',member.email);context=await edge.browser.newContext();const page=await context.newPage();let fetchToIdentity=false,blockedConnect=false;
 page.on('request',r=>{if(r.resourceType()==='fetch'&&new URL(r.url()).port==='8180')fetchToIdentity=true;});
 page.on('console',m=>{if(m.type()==='error'&&m.text().includes('connect-src'))blockedConnect=true;});
 await page.goto(web+'/account');const link=page.locator('a[href="/account/sign-in"]').first();assert.equal(await link.getAttribute('data-enhance-nav'),'false');
 await link.click();await page.waitForSelector('#username');await page.fill('#username',member.email);await page.fill('#password',member.password);await page.click('#kc-login');await page.waitForURL(url=>url.href.startsWith(web+'/account')||url.search.includes('TERMS_AND_CONDITIONS'));if(page.url().includes('TERMS_AND_CONDITIONS'))await page.click('[name=accept]');await page.waitForURL(web+'/account');
 const session=await page.request.get(web+'/bff/api/session',{headers:{'X-Swoovr-Bff':'1'}});assert.equal(session.status(),200);
 assert.equal(fetchToIdentity,false,'identity handoff must be a document navigation');assert.equal(blockedConnect,false,'identity hop must not need broader connect-src');
 if(process.env.SWOOVR_SCREENSHOTS){await page.goto(web+'/account/data');await page.waitForSelector('[data-data-panel=none]');await page.setViewportSize({width:390,height:900});await require('node:fs/promises').mkdir(process.env.SWOOVR_SCREENSHOTS,{recursive:true});await page.screenshot({path:require('node:path').join(process.env.SWOOVR_SCREENSHOTS,'data-390.png'),fullPage:true});}
 console.log('PASS rendered authentication navigation: 4 checks; actual local Keycloak/browser link and session, no external fetch/CSP connect violation.');
}finally{if(context)await context.close();await edge.close();}})().catch(e=>{console.error(e);process.exitCode=1});
