// Actual Edge keyboard/media/text-resize/rendered-theme/staff floor. No screen-reader certification.
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
const {managedEdge}=require('../auth/managed-edge.cjs');
const {web,admin,createStaff,staffToken,rootAdminToken,call}=require('../auth/local-tokens.cjs');
const palettes=require('../../src/Swoovr.Web/wwwroot/appearance-palettes.js'),audit=require('./audit.cjs');
if(!process.env.SWOOVR_TEST_APP_DATABASE)throw Error('Use owned runner.');
(async()=>{const edge=await managedEdge(chromium),contexts=[],failures=[];let checks=0;
const check=(ok,label)=>{checks++;if(!ok)failures.push(label);};
try{
 const context=await edge.browser.newContext({viewport:{width:390,height:844}});contexts.push(context);const page=await context.newPage();
 await page.goto(web+'/privacy');await page.waitForSelector('main h1');await page.waitForLoadState('networkidle');await page.locator('.skip-link').focus();
 check(await page.locator('.skip-link').evaluate(e=>{const r=e.getBoundingClientRect();return document.activeElement===e&&r.top>=0&&r.bottom<=innerHeight;}),'member skip link visible when focused');
 await page.keyboard.press('Enter');await page.waitForTimeout(300);check(await page.evaluate(()=>document.activeElement?.id==='main-content'&&location.pathname==='/privacy'),'member skip link moves actual focus to same-page main');
 for(const palette of palettes)for(const mode of ['light','dark']){
  await page.evaluate(({scheme,mode})=>{document.documentElement.dataset.scheme=scheme;document.documentElement.dataset.theme=mode;},{scheme:palette.id,mode});
  const problems=await page.evaluate(audit);check(!problems.length,`rendered ${palette.id}/${mode}: ${problems.join('; ')}`);
 }
 await page.evaluate(()=>{document.documentElement.dataset.scheme='standard';document.documentElement.dataset.theme='light';document.documentElement.style.fontSize='200%';});
 await page.setViewportSize({width:320,height:844});check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'member text resized200% at320px reflows');
 await page.evaluate(()=>document.documentElement.style.fontSize='');
 await page.emulateMedia({reducedMotion:'reduce',forcedColors:'active'});
 check(await page.evaluate(()=>matchMedia('(prefers-reduced-motion: reduce)').matches&&matchMedia('(forced-colors: active)').matches),'actual reduced-motion/forced-colors media active');
 check(await page.evaluate(()=>{const e=document.createElement('span');e.style.cssText='animation-duration:2s;transition-duration:2s';document.body.append(e);const s=getComputedStyle(e),ok=parseFloat(s.animationDuration)<=0.001&&parseFloat(s.transitionDuration)<=0.001;e.remove();return ok;}),'global reduced-motion duration override');
 await page.locator('.skip-link').focus();check(await page.locator('.skip-link').evaluate(e=>getComputedStyle(e).outlineStyle!=='none'),'member focus outline survives forced colors');
 await page.emulateMedia({reducedMotion:'no-preference',forcedColors:'none'});
 const root=await rootAdminToken(edge.browser),staff=await createStaff('a11y-admin');
 assert.equal((await call(root,'/api/admin/invitations',{method:'POST',body:{email:staff.email,role:'admin'}})).status,201);
 const token=await staffToken(edge.browser,staff);assert.equal((await call(token,'/api/session')).status,200);
 const staffContext=await edge.browser.newContext({viewport:{width:390,height:844}});contexts.push(staffContext);const staffPage=await staffContext.newPage();
 await staffPage.goto(admin+'/sign-in');await staffPage.fill('#username',staff.email);await staffPage.fill('#password',staff.password);await Promise.all([staffPage.waitForURL(admin+'/'),staffPage.click('#kc-login')]);
 await staffPage.waitForLoadState('networkidle');const asset=await staffContext.request.get(await staffPage.locator('script[src*="skip-main"]').getAttribute('src').then(src=>new URL(src,admin).href));check(asset.status()===200&&/javascript/.test(asset.headers()['content-type']||''),'staff bypass asset is actual JavaScript');check((await asset.body()).length>0,'staff bypass asset has nonempty body');const skip=staffPage.locator('.skip-link');check(await skip.count()===1,'staff layout provides skip link');
 if(await skip.count()){await skip.focus();await staffPage.keyboard.press('Enter');await staffPage.waitForTimeout(300);check(await staffPage.evaluate(()=>document.activeElement?.id==='main-content'),'staff skip link moves focus to main');}
 for(const route of ['/','/accounts','/cases','/appeals','/media','/holds','/registry','/code-reports']){
  await staffPage.goto(admin+route);await staffPage.waitForSelector('main h1');
  for(const width of [390,320]){await staffPage.setViewportSize({width,height:844});const problems=await staffPage.evaluate(audit);check(!problems.length,`staff ${route}@${width}: ${problems.join('; ')}`);}
 }
 await staffPage.evaluate(()=>document.documentElement.style.fontSize='200%');check(await staffPage.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'staff text resized200% at320px reflows');
 await staffPage.evaluate(()=>document.documentElement.style.fontSize='');if(await skip.count())await skip.focus();
 if(process.env.SWOOVR_SCREENSHOTS){await fs.mkdir(process.env.SWOOVR_SCREENSHOTS,{recursive:true});await staffPage.screenshot({path:path.join(process.env.SWOOVR_SCREENSHOTS,'staff-accessibility-320.png'),fullPage:false});}
 assert.equal(failures.length,0,failures.join('\n'));
 console.log(`PASS: expanded accessibility (${checks} checks; ${palettes.length*2} rendered theme modes, keyboard,200% text, reduced motion, forced colors,8 staff pages).`);
}finally{for(const context of contexts)await context.close();await edge.close();}})().catch(e=>{console.error(e.message);process.exitCode=1;});
