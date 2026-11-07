// Real local member journey; every identity and image below is a safe synthetic fixture.
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),crypto=require('node:crypto');
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
module.exports=async({page,context,sql,ownedMedia,root})=>{
 const base='http://localhost:15881',get=async(route)=>{const r=await context.request.get(base+'/bff'+route,{headers:{'X-Swoovr-Bff':'1'}});assert(r.ok(),route+' '+r.status());return r.json()};
 await page.goto(base+'/account');await page.locator('[data-account-panel=ready]').waitFor();
 assert.match(await page.locator('h1').innerText(),/Your account/);
 await page.locator('[data-phone-required=true]').waitFor();
 const gated=await context.request.get(base+'/bff/api/me/profile',{headers:{'X-Swoovr-Bff':'1'}});assert.equal(gated.status(),403);assert.match(await gated.text(),/phone-verification-required/);
 assert.equal(await page.locator('.settings-list h2').count(),3);
 await page.fill('#phone-number','0491 570 006');await page.locator('[data-phone-send] button[type=submit]').click();await page.locator('#phone-code').waitFor();
 let code;for(let i=0;i<30&&!code;i++){const list=await(await fetch('http://localhost:18025/api/v1/messages')).json();if(list.messages?.[0]){const mail=await(await fetch('http://localhost:18025/api/v1/message/'+list.messages[0].ID)).json();code=/Your Swoovr code is (\d{6})/.exec(mail.Text)?.[1]}if(!code)await sleep(200)}assert(code,'Owned simulated phone email');
 await page.fill('#phone-code',code);await page.locator('[data-phone-confirm] button').click();await page.locator('[data-phone=verified]').waitFor();
 await page.fill('#dob','1990-01-01');await page.locator('[data-age-form] [type=checkbox]').check();await page.locator('[data-age-form] button[type=submit]').click();await page.locator('[data-dob-locked=saved]').waitFor();
 await page.goto(base+'/me/profile');await page.locator('[data-profile-editor=ready]').waitFor();await page.locator('#display-name').waitFor();
 assert((await page.locator('.member-settings').boundingBox()).width>700);
 await page.fill('#display-name','Fictional Member');await page.fill('#area','Adelaide CBD');await page.fill('#about','A fictional adult local testing account.');
 await page.fill('#home-city','Adelaide');await page.locator('[data-city="2078025"]').click();
 const label=await page.locator('[data-city-search] > label').boundingBox(),value=await page.locator('[data-city-picked]').boundingBox();assert(label.y+label.height<=value.y,'City label stays above selected value');
 await page.locator('[data-core-form] button[type=submit]').click();await page.locator('[data-core-status=saved]').waitFor();
 const friends=page.locator('[data-mode-card=friends]');await friends.locator('input[type=checkbox]').nth(0).check();await friends.locator('[data-mode-enabled]').count();await page.locator('[data-mode-card=friends][data-mode-enabled=true]').waitFor();
 await friends.locator('input[type=checkbox]').nth(1).uncheck();await page.locator('[data-mode-card=friends][data-mode-visible=false]').waitFor();
 await page.reload();await page.locator('[data-mode-card=friends][data-mode-visible=false]').waitFor();assert.equal(await page.inputValue('#display-name'),'Fictional Member');
 await page.goto(base+'/me/matching');await page.locator('[data-filter-form=dating]').waitFor();
 await page.fill('#min-age','24');await page.fill('#max-age','44');await page.locator('#max-age').blur();await page.selectOption('#filter-mode','friends');await page.locator('[data-filter-form=friends]').waitFor();
 await page.selectOption('#filter-mode','dating');await page.locator('[data-filter-form=dating]').waitFor();assert.equal(await page.inputValue('#min-age'),'24');assert.equal(await page.inputValue('#max-age'),'44');await page.locator('[data-filter-status=draft]').waitFor();
 const requiredChip=page.locator('[data-required-chip]').first(),excludedChip=page.locator('[data-excluded-chip]').nth(1);
 const requiredId=await requiredChip.getAttribute('data-required-chip'),excludedId=await excludedChip.getAttribute('data-excluded-chip');
 await requiredChip.check();assert(await page.locator(`[data-excluded-chip="${requiredId}"]`).isDisabled());await excludedChip.check();
 await page.getByText('All of these',{exact:true}).click();
 await page.locator('[data-filter-form] button[type=submit]').click();await page.locator('[data-filter-status=saved]').waitFor();
 const datingSaved=await get('/api/me/modes/dating/filters');assert.equal(datingSaved.requiredMatch,'all');assert.deepEqual(datingSaved.requiredChips,[requiredId]);assert.deepEqual(datingSaved.excludedChips,[excludedId]);
 const friendsSaved=await get('/api/me/modes/friends/filters');assert.notEqual(friendsSaved.minAge,24,'Separate mode persistence');
 await page.reload();await page.locator('[data-filter-form=dating]').waitFor();assert.equal(await page.inputValue('#min-age'),'24');
 assert.equal(await page.locator('[data-plus-filters]').getAttribute('data-plus-filters'),'off');assert(await page.locator('#distance').isDisabled());
 // A transient read failure must retain the cached draft; a delayed switch must not expose stale fields.
 await page.fill('#min-age','25');await page.locator('#min-age').blur();
 await page.selectOption('#filter-mode','friends');await page.locator('[data-filter-form=friends]').waitFor();
 await page.route('**/bff/api/me/modes/dating/filters',async route=>{await sleep(600);await route.fulfill({status:503,contentType:'application/problem+json',body:'{"code":"test-owned-transient"}'});},{times:1});
 await page.selectOption('#filter-mode','dating');assert.equal(await page.locator('[data-filter-form]').count(),0,'No stale previous-mode fields during load');
 await page.getByText('We couldn’t load this mode’s filters.',{exact:false}).waitFor();
 await page.selectOption('#filter-mode','friends');await page.locator('[data-filter-form=friends]').waitFor();
 await page.selectOption('#filter-mode','dating');await page.locator('[data-filter-form=dating]').waitFor();assert.equal(await page.inputValue('#min-age'),'25','Draft survives failed reload');
 await page.locator('[data-filter-form] button[type=submit]').click();await page.locator('[data-filter-status=saved]').waitFor();
 // Author only synthetic photo fixture state in the disposable database. This is not a live moderation result.
 const account=await sql("SELECT id FROM accounts WHERE email LIKE 'swoovr-member-settings-%@example.test'");assert(account);
 const bytes=await require('../media/synthetic-images.cjs').synthetic(page,{kind:'gradient',width:320,height:240,type:'image/jpeg',seed:950,label:'SAFE SYNTHETIC FIXTURE'});
 await fs.mkdir(path.join(ownedMedia,'photos'),{recursive:true});const ids=[];
 for(let i=0;i<2;i++){const id=crypto.randomUUID(),key='photos/'+id.replaceAll('-',''),hash=crypto.createHash('sha256').update(bytes).digest('hex');ids.push(id);await fs.writeFile(path.join(ownedMedia,key),bytes,{mode:0o600});await sql(`INSERT INTO media_items(id,account_id,status,object_key,sha256,upload_sha256,content_type,width,height,bytes,created_at,screen_attempts,screened_at,screener,policy_version,known_content_checked) VALUES('${id}','${account}','Passed','${key}',decode('${hash}','hex'),decode('${hash}','hex'),'image/jpeg',320,240,${bytes.length},now(),1,now(),'synthetic-safe-fixture','fixture-only',true)`)}
 await sql(`INSERT INTO media_placements(account_id,scope,position,media_id) VALUES('${account}','main',0,'${ids[0]}')`);
 await page.goto(base+'/me/photos');await page.locator('[data-photos="2"]').waitFor();assert.equal(await page.locator('[data-mode-photos]').count(),1);
 // #231: a mode shows only its own photos; photos are added from a picker, removed with ×, and made main from the viewer.
 const addTo=async(mode,id)=>{await page.locator(`[data-photo-mode=${mode}]`).click();await page.locator('[data-add-mode-photos]').click();await page.locator(`[data-pick-photo="${id}"]`).click();await page.locator('[data-pick-done]').click();};
 await addTo('dating',ids[1]);await page.locator('[data-mode-photos=dating][data-mode-photo-count="1"]').waitFor();
 await page.locator('[data-photo-mode=friends]').click();await page.locator('[data-mode-photos=friends][data-mode-photo-count="0"]').waitFor();await addTo('friends',ids[1]);await page.locator('[data-mode-photos=friends][data-mode-photo-count="1"]').waitFor();
 await page.locator('[data-photo-mode=dating]').click();await page.locator(`[data-mode-photos=dating] [data-remove-mode-photo="${ids[1]}"]`).click();await page.locator('[data-mode-photos=dating][data-mode-photo-count="0"]').waitFor();await page.locator('[data-photo-mode=friends]').click();await page.locator('[data-mode-photos=friends][data-mode-photo-count="1"]').waitFor();await page.locator(`[data-photo="${ids[0]}"]`).waitFor();
 await page.locator(`[data-mode-photos=friends] [data-assigned-photo="${ids[1]}"] .ph-open`).click();await page.locator(`[data-mode-make-main="${ids[1]}"]`).click();await page.locator(`[data-mode-photos=friends] [data-assigned-photo="${ids[1]}"] .ph-badge`).getByText("Main",{exact:true}).waitFor();
 const out=path.join(root,'docs/delivery/evidence/2026-11-02-member-settings');await fs.mkdir(out,{recursive:true});
 for(const route of ['account','me/profile','me/matching','me/photos']){
  await page.goto(base+'/'+route);await page.waitForTimeout(700);
  for(const theme of ['light','dark'])for(const width of [375,768,1024,1440]){
   await page.setViewportSize({width,height:1000});await page.evaluate(t=>document.documentElement.dataset.theme=t,theme);await page.evaluate(()=>document.fonts.ready);await page.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));await sleep(150);
   assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),route+' '+width+' '+theme+' overflow');
   if((width===375&&theme==='dark')||(width===1440&&theme==='light'))await page.screenshot({path:path.join(out,route.replaceAll('/','-')+'-'+theme+'-'+width+'.png'),fullPage:true});
  }
 }
 console.log('PASS: phone refusal and simulated confirmation, age declaration, saved name/city, independent visibility, matching drafts/save and failed/delayed loads, Any/All and exclusions, Plus refusal, no inherited shared main, independent mode main and photo assignments; 32 responsive/theme combinations.');
};
