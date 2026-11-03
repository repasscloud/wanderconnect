// Called by an isolated runner with a real, fictional local staff session. No credentials are captured.
const assert = require('node:assert/strict');
module.exports = async ({page, base, out}) => {
 let checks=0;
 const shot=async options=>{await page.evaluate(async()=>{await document.fonts.ready;await new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)))});await page.waitForTimeout(150);return page.screenshot(options);};
 const check=(value,message)=>{assert(value,message);checks++;};
 const overflow=()=>page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth);
 check(await page.locator('#sidebar-home .admin-sidebar nav a').count()===19,'every original destination plus overview, disclosures and registration settings');
 check(await page.locator('#sidebar-home .nav-link[aria-current=page]').getAttribute('href')==='/cases','queue active indicator');
 check(await page.locator('table tbody tr').count()===14,'dense fictional queue rendered');
 for(const width of [1440,1100,390]){
  await page.setViewportSize({width,height:900});check(await overflow(),'page reflow '+width+' '+await page.evaluate(()=>[...document.querySelectorAll('body *')].filter(e=>e.getBoundingClientRect().right>innerWidth+1).slice(0,5).map(e=>e.tagName+'.'+e.className).join(' | ')));
  if(width===390)await page.waitForSelector('#navigation-drawer .admin-sidebar',{state:'attached'});
  if(width>800)check(await page.locator('#sidebar-home .admin-sidebar').isVisible(),'persistent desktop sidebar');
  if(width===1100)await shot({path:out+'/after-queue-1100.png',fullPage:true});
 }
 const menu=page.getByRole('button',{name:'Open navigation'});
 check(await menu.isVisible(),'narrow hamburger');
 await menu.focus();await page.keyboard.press('Enter');
 check(await page.locator('#navigation-drawer').evaluate(e=>e.open),'keyboard opens native modal drawer');
 check(await menu.getAttribute('aria-expanded')==='true','expanded state');
 for(let i=0;i<24;i++)await page.keyboard.press('Tab');
 check(await page.evaluate(()=>document.querySelector('#navigation-drawer').contains(document.activeElement)),'drawer contains keyboard focus');
 await shot({path:out+'/after-drawer-390.png'});
 await page.keyboard.press('Escape');check(await menu.evaluate(e=>e===document.activeElement),'Escape returns trigger focus');
 await menu.click();await page.locator('#navigation-drawer a[href="/appeals"]').click();await page.waitForURL(base+'/appeals');await page.waitForSelector('#navigation-drawer a[href="/appeals"][aria-current=page]',{state:'attached'});
 await page.getByRole('button',{name:'Open navigation'}).click();
 check(await page.locator('#navigation-drawer .nav-link[aria-current=page]').getAttribute('href')==='/appeals','enhanced drawer navigation updates active page');
 await page.keyboard.press('Escape');await page.goto(base+'/cases');
 await menu.click();await page.setViewportSize({width:1100,height:900});
 await page.waitForFunction(()=>!document.querySelector('#navigation-drawer').open);
 check(await page.locator('#navigation-drawer').evaluate(e=>!e.open),'desktop resize closes modal');
 check(await page.locator('#sidebar-home .admin-sidebar').count()===1,'navigation restored to desktop');
 await page.setViewportSize({width:1440,height:1000});
 await page.locator('table tbody tr a').first().click();await page.waitForSelector('[data-action-form]');
 check(await page.locator('#sidebar-home .nav-link[aria-current=page]').getAttribute('href')==='/cases','nested case keeps queue selection');
 await shot({path:out+'/after-case-1440.png',fullPage:true});
 await page.selectOption('select[name="ActForm.Action"]','warn');await page.click('[data-act]');
 await page.waitForSelector('[data-result]:not([data-result=ok])');
 await shot({path:out+'/after-validation-1440.png',fullPage:true});
 check(await page.locator('[data-sanctions=none]').count()===1,'missing reason refuses sanction server-side');
 await page.selectOption('select[name="ActForm.Action"]','warn');
 await page.fill('textarea[name="ActForm.Reason"]','Fictional moderation workflow acceptance reason.');
 await page.click('[data-act]');await page.waitForSelector('[data-result=ok]');
 check(await page.locator('[data-sanction-kind=warning]').count()===1,'reasoned warning records through unchanged form');
 await page.setViewportSize({width:390,height:844});check(await overflow(),'complex case form reflow');
 await shot({path:out+'/after-case-390.png',fullPage:true});
 await page.setViewportSize({width:1440,height:1000});
 await page.goto(base+'/access');await page.waitForSelector('[data-request-access]');await shot({path:out+'/after-access-1440.png',fullPage:true});
 const form=page.locator('[data-request-access]');
 await form.locator('select[name=capability]').selectOption('termination');
 await form.locator('textarea[name=purpose]').fill('Fictional layout acceptance review of time-limited access.');
 await form.locator('button[type=submit]').click();await page.waitForSelector('[data-result=ok]');checks++;
 check(await page.locator('[data-grant-capability="termination"][data-grant-active=yes]').count()===1,'SSR grant form retains real behaviour');
 for(const route of ['/','/accounts','/registry','/holds','/law-enforcement','/appeals','/media','/beta','/support','/contact-history','/security','/safety','/beta-scorecard','/code-reports','/dob-reviews','/disclosures']){
  await page.goto(base+route);check(await page.locator('.admin-header').count()===1,'single header '+route);check((await page.locator('.admin-main').count())===1,'SSR shell '+route);
  await page.setViewportSize({width:390,height:844});check(await overflow(),'narrow reflow '+route);
 }
 await page.goto(base+'/cases');
 await page.locator('.table-scroll').focus();await page.keyboard.press('ArrowRight');
 check(await page.locator('.table-scroll').evaluate(e=>e.scrollWidth>e.clientWidth),'table scroll is local at narrow width');
 await shot({path:out+'/after-queue-390.png'});
 await page.setViewportSize({width:1440,height:1000});await page.goto(base+'/');await shot({path:out+'/after-overview-1440.png',fullPage:true});
 await page.keyboard.press('Control+Home');await page.locator('.skip-link').focus();await page.keyboard.press('Enter');
 check(await page.locator('#main-content').evaluate(e=>e===document.activeElement),'skip link focuses workspace');
 await page.emulateMedia({forcedColors:'active',reducedMotion:'reduce'});check(await overflow(),'forced colours/reduced motion');
 await page.evaluate(()=>document.documentElement.style.fontSize='200%');
 await page.setViewportSize({width:390,height:844});check(await overflow(),'200 percent text narrow reflow');
 await page.evaluate(()=>document.documentElement.style.fontSize='');
 await page.locator('.staff-menu summary').focus();await page.keyboard.press('Enter');
 check(await page.getByRole('button',{name:'Sign out',exact:true}).isVisible(),'keyboard staff account actions');
 await page.keyboard.press('Escape');check(await page.locator('.staff-menu').evaluate(e=>!e.open),'Escape closes staff menu');
 const fallback=await page.context().browser().newContext({storageState:await page.context().storageState(),javaScriptEnabled:false,viewport:{width:390,height:844}});
 try {const staticPage=await fallback.newPage();await staticPage.goto(base+'/cases');check(await staticPage.locator('#sidebar-home .admin-sidebar').isVisible(),'navigation works without JavaScript');} finally {await fallback.close();}
 console.log(`PASS: ${checks} admin shell/browser checks.`);
};
