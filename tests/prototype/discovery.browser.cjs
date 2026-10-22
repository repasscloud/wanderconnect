const assert=require('node:assert/strict');
const fs=require('node:fs/promises');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
const base=process.env.SWOOVR_WEB_URL||'http://127.0.0.1:5081';
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});
 try{
  await fs.mkdir('.local/discovery-browser',{recursive:true,mode:0o700});
  const page=await browser.newPage({viewport:{width:1280,height:1000}});
  const errors=[];page.on('pageerror',error=>errors.push(error.message));
  await page.goto(base+'/prototype/people');
  const names=async()=>page.locator('.person-card h2').allTextContents();
  assert.deepEqual(await names(),['Jamie, 34','Rowan, 40','Casey, 29']);
  await page.getByLabel('Hide sparse profiles',{exact:true}).check();
  await page.getByRole('button',{name:'Apply demo settings',exact:true}).click();
  await page.waitForURL(/hideSparse=true/);assert.deepEqual(await names(),['Jamie, 34','Casey, 29']);
  await page.getByLabel('Browse incognito',{exact:true}).check();
  await page.getByRole('button',{name:'Apply demo settings',exact:true}).click();
  await page.waitForURL(/incognito=true/);assert.deepEqual(await names(),['Jamie, 34']);
  await page.getByRole('link',{name:'Jamie, 34',exact:true}).click();
  await page.getByRole('heading',{name:'Jamie, 34',exact:true}).waitFor();
  await page.getByRole('button',{name:'Check demo contact eligibility',exact:true}).click();
  await page.getByRole('status').waitFor();assert.match(await page.getByRole('status').innerText(),/unavailable/);
  await page.getByLabel('Show my fictional profile for this contact check',{exact:true}).check();
  await page.getByRole('button',{name:'Check demo contact eligibility',exact:true}).click();
  await page.waitForURL(/disclose=true/);assert.match(await page.getByRole('status').innerText(),/eligible.*No request or message was sent/);
  await page.goto(base+'/prototype/people?travel=upcoming');
  assert.equal(await page.getByText('Contact unavailable with these demo settings',{exact:true}).count(),3);
  await page.getByRole('link',{name:'Jamie, 34',exact:true}).click();
  await page.getByRole('button',{name:'Check demo contact eligibility',exact:true}).click();
  await page.getByRole('status').waitFor();assert.match(await page.getByRole('status').innerText(),/unavailable/);
  await page.goto(base+'/prototype/people?travel=arrived');
  assert.equal(await page.getByText('Contact eligible in this demo',{exact:true}).count(),3);
  await page.getByLabel('Also require friendship',{exact:true}).check();
  await page.getByLabel('Required intention match',{exact:true}).selectOption('all');
  await page.getByRole('button',{name:'Apply demo settings',exact:true}).click();
  await page.waitForURL(/match=all/);assert.deepEqual(await names(),['Jamie, 34']);
  await page.getByLabel('Also require activity companion',{exact:true}).check();
  await page.getByRole('button',{name:'Apply demo settings',exact:true}).click();
  await page.getByRole('heading',{name:'No people match these settings',exact:true}).waitFor();
  await page.goto(base+'/prototype/people/10000000-0000-0000-0000-000000000001');
  await page.getByRole('heading',{name:'Profile unavailable',exact:true}).waitFor();
  assert.equal((await page.locator('body').innerText()).includes('Morgan'),false);
  for (const query of ['incognito=maybe','hideSparse=maybe','requireFriendship=maybe','incognito=true&incognito=false','travel=unknown']) {
    const response=await page.goto(base+'/prototype/people?'+query);
    assert.equal(response.status(),200);assert.match(await page.getByRole('status').innerText(),/listed demo settings/);
  }
  for (const query of ['incognito=maybe','checkContact=maybe','disclose=maybe']) {
    const response=await page.goto(base+'/prototype/people/10000000-0000-0000-0000-000000000002?'+query);
    assert.equal(response.status(),200);await page.getByRole('heading',{name:'Profile unavailable',exact:true}).waitFor();
  }
  await page.goto(base+'/settings/appearance');
  await page.getByRole('radio',{name:'Union Jack',exact:true}).check();
  await page.getByRole('radio',{name:'Dark',exact:true}).check();
  await page.goto(base+'/prototype/people');
  assert.equal(await page.evaluate(()=>document.documentElement.dataset.scheme),'union');
  assert.equal(await page.evaluate(()=>document.documentElement.dataset.theme),'dark');
  await page.screenshot({path:'.local/discovery-browser/union-dark-desktop.png',fullPage:true});
  await page.setViewportSize({width:320,height:900});
  await page.evaluate(()=>document.documentElement.style.fontSize='200%');
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),true,'Preview reflows at 320px/200%');
  await page.screenshot({path:'.local/discovery-browser/union-dark-320-large.png',fullPage:true});
  await page.evaluate(()=>document.documentElement.style.fontSize='100%');
  await page.screenshot({path:'.local/discovery-browser/union-dark-320.png',fullPage:true});
  assert.deepEqual(errors,[]);
  console.log('PASS: actual Edge fictional grid/profile, sparse/incognito/ANY-ALL filters, disclosure/contact negatives, upcoming versus simulated arrival, hidden direct profile, persisted appearance and 320px/200% reflow.');
 }finally{await browser.close()}
})().catch(error=>{console.error(error.message);process.exitCode=1});
