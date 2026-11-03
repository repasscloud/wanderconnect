const assert=require('node:assert/strict');
module.exports=async({page,base})=>{
 for(const width of [375,768,1440]){
  await page.setViewportSize({width,height:900});
  for(const route of ['/media','/registry','/holds','/law-enforcement','/identity','/access','/security']){
   await page.goto(base+route);
   assert.equal(await page.locator('[data-desktop-required]').isVisible(),width<768,'phone recovery '+route+' '+width);
   assert.equal(await page.locator('[data-workflow]').isVisible(),width>=768,'tablet/desktop workflow '+route+' '+width);
   if(width<768)assert.equal(await page.locator('[data-desktop-required] a').getAttribute('href'),'/');
  }
 }
 await page.setViewportSize({width:1440,height:900});
 for(const route of ['/support','/accounts','/appeals','/dob-reviews','/media','/contact-history']){
  await page.goto(base+route);
  assert.doesNotMatch(await page.locator('[data-workflow]').innerText(),/\(D\d{2,3}(?:, D\d{2,3})*\)|\(#\d+\)/,'no development references '+route);
 }
 await page.goto(base+'/support');assert.equal(await page.locator('[data-workflow] > p.help').first().innerText(),'Find a report using its support reference.');
 console.log('PASS: phone recovery and full tablet/desktop presentation. Backend authorisation remains separately enforced.');
};
