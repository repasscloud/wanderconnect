const assert=require('node:assert/strict');
const base='http://localhost:15882';
exports.refused=async page=>{
 await page.goto(base+'/accounts?email=fictional.design.0%40example.test');
 assert.equal(await page.locator('[data-accounts="none"]').count(),0,'API refusal must not be shown as no matching account');
 assert.equal(await page.locator('[data-accounts="refused"]').count(),1,'refusal is explicit');
};
exports.found=async page=>{
 for(const email of ['fictional.design.0@example.test',' FICTIONAL.DESIGN.0@example.test ']){
  await page.goto(base+'/accounts?email='+encodeURIComponent(email));
  assert.equal(await page.locator('[data-account-link]').count(),1,'canonical email finds account without profile');
 }
 await page.goto(base+'/accounts?email=fictional.absent%40example.test');
 assert.equal(await page.locator('[data-accounts="none"]').count(),1,'successful empty lookup has no-match wording');
 await page.setViewportSize({width:375,height:812});
 assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'lookup reflows');
 console.log('PASS: actual Edge lookup refusal, exact/normalised email, profile-free account and genuine no-match.');
};
