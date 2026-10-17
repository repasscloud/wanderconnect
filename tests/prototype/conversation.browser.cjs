const assert=require('node:assert/strict');
const fs=require('node:fs/promises');
const {chromium}=require(process.env.DATINGAPP_PLAYWRIGHT_PATH||'playwright');
const base=process.env.DATINGAPP_WEB_URL||'http://127.0.0.1:5301';
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});
 try{
  const context=await browser.newContext({viewport:{width:1280,height:1000}});const page=await context.newPage();page.setDefaultTimeout(10000);
  const errors=[];page.on('pageerror',error=>errors.push(error.name));
  await page.goto(base+'/prototype/conversation');
  await page.getByRole('heading',{name:'A conversation starts with a choice.',exact:true}).waitFor();
  const create=async(travel='local')=>{
   await page.goto(base+'/prototype/conversation');await page.getByLabel('Simulated presence',{exact:true}).selectOption(travel);
   await page.getByRole('button',{name:'Start fictional scenario',exact:true}).click();await page.waitForURL(/id=.*actor=alex/);
  };
  const click=async(name)=>{await page.getByRole('button',{name,exact:true}).click();};
  await create();const first=page.url();
  await click('Send sample request');await page.getByRole('heading',{name:'Waiting for Jamie',exact:true}).waitFor();
  assert.equal(await page.getByRole('button',{name:'Send sample reply',exact:true}).count(),0);
  const stale=await page.context().newPage();await stale.goto(page.url());
  await page.getByRole('link',{name:'View as fictional Jamie',exact:true}).click();
  await click('Accept request');await page.getByRole('heading',{name:'Conversation accepted',exact:true}).waitFor();
  await click('Send sample reply');assert.equal(await page.locator('.conversation-message').count(),2);
  await stale.getByRole('button',{name:'Close conversation',exact:true}).click();
  await stale.getByRole('status').waitFor();assert.match(await stale.getByRole('status').innerText(),/changed|unavailable/i);await stale.close();
  // Native form antiforgery is required by the web host, independently of API simulation.
  const csrf=await page.request.post(page.url(),{form:{_handler:'ScenarioAction','Input.Action':'block','Input.Revision':'3'}});assert.equal(csrf.status(),400);
  await click('Block in this scenario');await page.getByRole('heading',{name:'Conversation unavailable',exact:true}).waitFor();
  assert.equal(await page.locator('.conversation-message').count(),0);
  await page.getByRole('link',{name:'View as fictional Alex',exact:true}).click();
  assert.equal(await page.locator('.conversation-message').count(),0);
  assert.equal(await page.getByRole('button',{name:'Close conversation',exact:true}).count(),0);
  await create();await click('Send sample request');await click('Close conversation');await page.getByRole('heading',{name:'Conversation closed',exact:true}).waitFor();
  await create();await click('Send sample request');await page.getByRole('link',{name:'View as fictional Jamie',exact:true}).click();await click('Decline request');
  await page.getByRole('heading',{name:'Request declined',exact:true}).waitFor();assert.equal(await page.getByRole('button',{name:'Send sample request',exact:true}).count(),0);
  await create('upcoming');await click('Send sample request');await page.getByRole('status').waitFor();assert.match(await page.getByRole('status').innerText(),/changed|unavailable/i);
  await create('arrived');await click('Send sample request');await page.getByRole('heading',{name:'Waiting for Jamie',exact:true}).waitFor();
  const current=page.url();
  for(const query of ['?id=bad&actor=alex','?actor=admin','?id=bad&id=other']){
   await page.goto(base+'/prototype/conversation'+query);await page.getByRole('heading',{name:'Scenario unavailable',exact:true}).waitFor();
  }
  await page.goto(current);
  await page.setViewportSize({width:320,height:900});await page.evaluate(()=>document.documentElement.style.fontSize='200%');
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true);
  await fs.mkdir('.local/conversation-browser',{recursive:true,mode:0o700});
  await page.screenshot({path:'.local/conversation-browser/narrow-top.png'});
  await page.getByRole('button',{name:'Block in this scenario',exact:true}).scrollIntoViewIfNeeded();
  await page.screenshot({path:'.local/conversation-browser/narrow-controls.png'});
  assert.equal(await page.locator('header').count(),1);
  await page.setViewportSize({width:1280,height:1000});await page.goto(current);
  await page.screenshot({path:'.local/conversation-browser/pending.png',fullPage:true});
  await page.getByRole('link',{name:'View as fictional Jamie',exact:true}).focus();await page.keyboard.press('Enter');await page.waitForURL(/actor=jamie/);
  await create();await page.getByRole('link',{name:'View as fictional Jamie',exact:true}).click();await click('Send sample request');
  await page.getByRole('heading',{name:'Waiting for Alex',exact:true}).waitFor();
  await page.getByRole('link',{name:'View as fictional Alex',exact:true}).click();await click('Accept request');
  await page.getByRole('heading',{name:'Conversation accepted',exact:true}).waitFor();
  await page.goto(base+'/settings/appearance');
  await page.getByRole('radio',{name:'Cobalt social',exact:true}).check();await page.getByRole('radio',{name:'Dark',exact:true}).check();
  await page.goto(current);assert.equal(await page.evaluate(()=>document.documentElement.dataset.scheme),'cobalt');assert.equal(await page.evaluate(()=>document.documentElement.dataset.theme),'dark');
  await page.screenshot({path:'.local/conversation-browser/cobalt-dark.png',fullPage:true});
  await fs.writeFile('.local/conversation-browser/last.json',JSON.stringify({url:current}));
  assert.deepEqual(errors,[]);
  console.log('PASS: actual Edge fictional connection request/accept/reply/block/close/decline, presence negatives, stale revision, CSRF, malformed query, keyboard and320px/200% reflow.');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1});
