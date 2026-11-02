const assert=require('node:assert/strict');
module.exports=async({page,kc,userId})=>{
 const base='http://localhost:15882';
 const anonymous=await page.context().browser().newContext();
 const signedOut=await anonymous.newPage();
 await signedOut.goto(base+'/signed-out');
 assert.equal(await signedOut.locator('main').count(),1,'signed-out page has a main landmark');
 assert.equal(await signedOut.locator('link[href="/admin.css"]').count(),1,'signed-out page uses shared branding');
 assert.equal(await signedOut.locator('.staff-menu').count(),0,'signed-out page has no active staff identity');
 await anonymous.close();

 await page.waitForTimeout(40000);
 await page.goto(base+'/accounts?email=fictional.design.0%40example.test');
 assert.equal(await page.locator('[data-account-link]').count(),1,'usable provider session renews the expired access token');
 // Revoke only this owned fictional user's provider sessions, then let its last access token expire.
 await kc('/users/'+userId+'/logout','POST');
 await page.waitForTimeout(40000);
 await page.goto(base+'/accounts?email=fictional.design.0%40example.test');
 await page.waitForSelector('#username');
 assert.equal(new URL(page.url()).port,'8180','failed renewal challenges before rendering an authenticated staff header');
 console.log('PASS: real provider short-lived access-token renewal and revoked-session rejection.');
};
