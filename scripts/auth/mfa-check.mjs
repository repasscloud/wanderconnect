// Optional real-browser MFA evidence; never logs enrollment secrets, codes or identities.
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {createHarness} from './harness.mjs';
import {issuer,request} from './security.mjs';
import {localRealmApi,privateRead,validateFixture} from './provision.mjs';
import {withTemporaryOtp} from './mfa.mjs';
import {totp} from './totp.mjs';
const require=createRequire(import.meta.url);
let stage='preflight';
async function run(){
 const {chromium}=require(process.env.DATINGAPP_PLAYWRIGHT_PATH||'playwright');
 const fixture=JSON.parse(await privateRead('.local/auth-fixture.json'));
 const api=await localRealmApi();
 validateFixture(fixture,await api('/users?exact=true&username='+encodeURIComponent(fixture.username)));
 const realm=await api('');
 assert.equal(realm.realm,'datingapp-dev');assert.equal(realm.otpPolicyType,'totp');assert.equal(realm.otpPolicyAlgorithm,'HmacSHA1');
 assert.equal(realm.otpPolicyDigits,6);assert.equal(realm.otpPolicyPeriod,30);assert.equal(realm.otpPolicyLookAheadWindow,1);
 assert.ok(!(await api('/users/'+fixture.userId+'/credentials')).some(c=>c.type==='otp'));
 let issued=0;
 const server=createHarness(async(url,options)=>{const value=await request(url,options);if(url===issuer+'/protocol/openid-connect/token'&&value?.id_token)issued++;return value;});
 let browser,interrupted=false;
 const cancel=()=>{interrupted=true;if(browser)void browser.close().catch(()=>{});};
 process.once('SIGINT',cancel);process.once('SIGTERM',cancel);
 try{
  await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(8280,'127.0.0.1',resolve)});
  browser=await chromium.launch({channel:'msedge',headless:true});
  async function password(page){
   stage='open harness for password login';
   await page.goto('http://127.0.0.1:8280/');await page.getByRole('button',{name:'Browser login / SSO',exact:true}).click();
   stage='provider password challenge';
   await page.locator('#username').fill(fixture.username);await page.locator('#password').fill(fixture.password);await page.locator('#kc-login').click();
  }
  async function complete(page){
   stage='validated password callback';
   await page.waitForURL('http://127.0.0.1:8280/');await page.locator('tbody tr').first().waitFor();
   assert.match(await page.locator('tbody tr').first().innerText(),/browser.*true/s);
   stage='provider logout after validated callback';
   await page.getByRole('button',{name:'End provider session',exact:true}).click();
   stage='wait for logout observation';
   await page.waitForFunction(()=>document.querySelectorAll('tbody tr').length===2);
   stage='assert logout rejection';
   assert.match(await page.locator('tbody tr').last().innerText(),/logout.*true/s);
  }
  stage='baseline password-only browser login';
  let page=await browser.newPage();await password(page);await complete(page);await page.context().close();
  stage='baseline issuance count';assert.equal(issued,1);assert.equal(interrupted,false);
  stage='owned fixture MFA preflight';
  await withTemporaryOtp(api,fixture,async({label})=>{
   stage='authenticator enrollment';page=await browser.newPage();await password(page);
   stage='MFA enrollment manual setup';
   await page.locator('#mode-manual').click();
   const secret=(await page.locator('#kc-totp-secret-key').innerText()).replace(/\s/g,'');
   const enrollment=totp(secret);
   await page.locator('#totp').fill(enrollment);await page.locator('#userLabel').fill(label);await page.locator('#saveTOTPBtn').click();
   await complete(page);await page.context().close();assert.equal(issued,2);
   stage='OTP missing and wrong-code negatives';page=await browser.newPage();await password(page);
   stage='configured OTP challenge';
   await page.locator('#otp').waitFor();
   await page.locator('#kc-otp-login-form').evaluate(form=>{form.noValidate=true;});
   const before=issued;
   async function submit(code){
    await page.locator('#otp').fill(code);
    await Promise.all([page.waitForResponse(r=>r.request().method()==='POST'&&r.url().startsWith(issuer+'/login-actions/')),page.locator('#kc-login').click()]);
    await page.waitForLoadState('domcontentloaded');
   }
   stage='provider empty OTP rejection';
   await submit('');await page.locator('#otp[aria-invalid="true"]').waitFor();assert.equal(issued,before);
   const now=Math.floor(Date.now()/1000);const valid=new Set([-30,0,30].map(delta=>totp(secret,now+delta)));
   let wrong='000000';for(let n=0;valid.has(wrong);n++)wrong=String(n+1).padStart(6,'0');
   stage='provider wrong OTP rejection';
   await submit(wrong);await page.locator('#otp[aria-invalid="true"]').waitFor();assert.equal(issued,before);
   stage='password plus OTP browser login';
   const current=totp(secret);const code=current===enrollment?totp(secret,Math.floor(Date.now()/1000)+30):current;
   await submit(code);await complete(page);await page.context().close();assert.equal(issued,3);
  });
  stage='restored password-only browser login';page=await browser.newPage();await password(page);await complete(page);await page.context().close();assert.equal(issued,4);
  console.log('PASS: actual Edge password-only baseline, temporary TOTP enrollment, provider rejection of missing/wrong OTP without token issuance, password+OTP code/PKCE callback validation, owned credential/action cleanup and password-only restoration. No recovery flow or real email ownership proved; privileged access disabled.');
 }finally{
  process.removeListener('SIGINT',cancel);process.removeListener('SIGTERM',cancel);
  if(browser)await browser.close();if(server.listening)await new Promise(resolve=>server.close(resolve));
 }
}
run().catch(error=>{const message=String(error.message);const category=['Timeout','Content Security Policy','Execution context was destroyed','Target page','strict mode violation','page.waitForFunction','page.context','page.locator'].find(v=>message.includes(v))??'unclassified';console.error('Local MFA investigation failed at '+stage+' ('+error.name+', '+category+'). No private details printed; inspect owned fixture cleanup before rerunning.');process.exitCode=1});
