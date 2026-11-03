// D293: owned synthetic storage/approved metadata fixtures test placement/projection, not screening accuracy.
const assert = require('node:assert/strict'), crypto = require('node:crypto'), fs = require('node:fs/promises'), path = require('node:path'), os = require('node:os');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { createMember, memberToken, cli, call, matchReady, sql, claims, web, api } = require('../auth/local-tokens.cjs');
const { synthetic } = require('./synthetic-images.cjs');
if (!process.env.SWOOVR_TEST_API_BASE || !process.env.SWOOVR_TEST_APP_DATABASE || process.env.Media__Store !== 'local' || !process.env.Media__Local__Path?.startsWith(os.tmpdir())) throw Error('Mode photos require owned local media/database runner');
(async () => {
 const edge = await require('../auth/managed-edge.cjs').managedEdge(chromium);
 try {
  const [owner, viewer] = await Promise.all(['mode-photo-owner','mode-photo-viewer'].map(createMember));
  await cli('beta','add',owner.email,viewer.email);
  const a=await memberToken(edge.browser,owner), b=await memberToken(edge.browser,viewer);
  const modes=['dating','friends','activities','business'];
  for(const [token,name] of [[a,'Fictional Photo Owner'],[b,'Fictional Photo Viewer']]){
   assert.equal((await call(token,'/api/me/profile',{method:'PUT',body:{displayName:name,homeCityId:2078025}})).status,200);await matchReady(token);
   for(const mode of modes)await call(token,'/api/me/modes/'+mode,{method:'PUT',body:{enabled:true,visible:true}});
  }
  const id=(await call(a,'/api/me/profile')).body.publicId,account=await sql(`SELECT id FROM accounts WHERE subject='${claims(a).sub}'`);
  const context=await edge.browser.newContext(), canvas=await context.newPage();
  try{
   const jpeg=await synthetic(canvas,{kind:'gradient',type:'image/jpeg',seed:108}),hash=crypto.createHash('sha256').update(jpeg).digest('hex');
   const ids=Array.from({length:60},()=>crypto.randomUUID());
   await fs.mkdir(path.join(process.env.Media__Local__Path,'photos'),{recursive:true});
   for(const media of ids){
    await fs.writeFile(path.join(process.env.Media__Local__Path,'photos',media.replaceAll('-','')),jpeg,{mode:0o600});
    await sql(`INSERT INTO media_items(id,account_id,status,object_key,sha256,upload_sha256,content_type,width,height,bytes,created_at,screen_attempts,known_content_checked,screener,screened_at) VALUES('${media}','${account}','Passed','photos/${media.replaceAll('-','')}',decode('${hash}','hex'),decode('${hash}','hex'),'image/jpeg',640,480,${jpeg.length},now()-interval '2 hours',1,false,'synthetic-fixture',now())`);
   }
   const place=(mode,mediaIds,mainMediaId=null)=>call(a,'/api/me/photos/placement',{method:'PUT',body:{scope:mode,mediaIds,mainMediaId}});
   let own=(await call(a,'/api/me/photos')).body;
   assert.equal(own.maxKept,60);assert.equal(own.maxPerMode,8);assert.equal(own.main,null);assert.deepEqual(own.mains,{});
   assert.equal((await place('main',[ids[0]],ids[0])).status,400);
   for(const [i,mode]of modes.entries()){
    assert.equal((await place(mode,[ids[i],ids[i+4]],ids[i])).status,200);
    const person=(await call(b,`/api/profiles/${id}?mode=${mode}`)).body;
    assert.equal(person.photo.id,ids[i]);assert.equal(person.modes[0].mainPhoto.id,ids[i]);
    for(const other of modes.filter(m=>m!==mode))assert.ok(!JSON.stringify(person).includes(ids[modes.indexOf(other)]));
   }
   assert.equal((await place('friends',[ids[0]],ids[2])).status,400,'main must be in gallery');
   assert.equal((await place('friends',ids.slice(0,9),ids[0])).status,400,'Free ninth refused');
   assert.equal((await place('friends',[ids[0],ids[0]],ids[0])).status,400,'duplicates refused');
   assert.equal((await call(b,'/api/me/photos/placement',{method:'PUT',body:{scope:'friends',mediaIds:[ids[0]],mainMediaId:ids[0]}})).status,409,'ownership enforced');
   await place('dating',[ids[0],ids[4]]);
   assert.equal((await call(b,`/api/profiles/${id}?mode=dating`)).body.photo,null,'no main means placeholder, never gallery fallback');
   assert.equal((await call(b,'/api/explore?mode=dating')).body.people.find(p=>p.id===id).photo,null,'still discoverable without main');
   await sql(`INSERT INTO entitlements(id,account_id,tier,source,starts_at,ends_at,created_at,reason) VALUES('${crypto.randomUUID()}','${account}','Premium','AdminGrant',now()-interval '1 day',now()+interval '1 day',now(),'Synthetic mode photo entitlement')`);
   assert.equal((await place('business',ids.slice(0,24),ids[0])).status,200);
   await sql(`UPDATE entitlements SET revoked_at=now() WHERE account_id='${account}'`);
   own=(await call(a,'/api/me/photos')).body;assert.equal(own.modes.business.length,24);assert.equal(own.maxPerMode,8);
   let person=(await call(b,`/api/profiles/${id}?mode=business`)).body;assert.equal(person.modes[0].photos.length,8);
   assert.equal((await sql(`SELECT count(*) FROM media_items WHERE account_id='${account}' AND removed_at IS NULL`)),'60','downgrade deletes no library objects');
   assert.equal((await place('business',ids.slice(0,24).reverse(),ids[23])).status,200,'reorder retained tail');
   person=(await call(b,`/api/profiles/${id}?mode=business`)).body;assert.equal(person.photo.id,ids[23]);assert.equal(person.modes[0].photos[0].id,ids[23]);
   const form=new FormData();form.set('file',new Blob([jpeg],{type:'image/jpeg'}),'synthetic.jpg');
   assert.equal((await fetch(api+'/api/me/photos',{method:'POST',headers:{Authorization:'Bearer '+a,'Swoovr-Version':'2026-10-28'},body:form})).status,409,'sixty-library cap');
   await canvas.goto(web+'/account/sign-in');await canvas.fill('#username',owner.email);await canvas.fill('#password',owner.password);await Promise.all([canvas.waitForURL(web+'/explore'),canvas.click('#kc-login')]);
   await canvas.goto(web+'/me/photos');await canvas.waitForSelector('[data-photos-panel=ready]');
   assert.equal(await canvas.locator('#photo-input').getAttribute('multiple'),'');
   for(const width of [375,768,1440]){await canvas.setViewportSize({width,height:1000});assert.ok(await canvas.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));}
   console.log('PASS: D293 owned mode main/placeholder/privacy,60 library,Free8/Premium24,downgrade retention/reorder,ownership and actual photo editor');
  }finally{await context.close();}
 }finally{await edge.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
