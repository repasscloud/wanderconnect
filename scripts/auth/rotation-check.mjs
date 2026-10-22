// Actual local browser/key-rotation investigation. Tokens/fixtures stay in process memory.
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { createHarness } from './harness.mjs';
import { client,issuer,request,validateIdToken } from './security.mjs';
import { localRealmApi,privateRead,validateFixture } from './provision.mjs';
import { withTemporarySigningKey } from './rotation.mjs';
const require=createRequire(import.meta.url);
async function run(){
  const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
  const fixture=JSON.parse(await privateRead('.local/swoovr-auth-fixture.json'));
  assert.equal(fixture.owner,'swoovr-local-investigation-v1');assert.equal(fixture.fictionalAdult,true);
  assert.equal(typeof fixture.userId,'string');
  let managementGrants=0;
  const api=await localRealmApi({provider:async(url,options)=>{if(url.includes('/realms/master/'))managementGrants++;return request(url,options);}});
  validateFixture(fixture,await api('/users?exact=true&username='+encodeURIComponent(fixture.username)));
  const captured=[];
  const server=createHarness(async(url,options)=>{
    const value=await request(url,options);
    if(url===issuer+'/protocol/openid-connect/token' && value?.id_token)captured.push(value.id_token);
    return value;
  });
  let browser;
  let interrupted=false;
  const cancel=()=>{interrupted=true;if(browser)void browser.close().catch(()=>{});};
  process.once('SIGINT',cancel);process.once('SIGTERM',cancel);
  try{
    await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(8280,'127.0.0.1',resolve)});
    browser=await chromium.launch({channel:'msedge',headless:true});
    const page=await browser.newPage();
    await page.goto('http://127.0.0.1:8280/');
    const authorization=page.waitForRequest(r=>r.url().startsWith(issuer+'/protocol/openid-connect/auth?'));
    await page.getByRole('button',{name:'Browser login / SSO',exact:true}).click();
    const nonce=new URL((await authorization).url()).searchParams.get('nonce');
    await page.locator('#username').fill(fixture.username);await page.locator('#password').fill(fixture.password);
    await page.locator('#kc-login').click();await page.waitForURL('http://127.0.0.1:8280/');
    await page.locator('tbody tr').first().waitFor();
    assert.equal(captured.length,1);
    const options={issuer,client,nonce};
    const firstToken=captured[0];
    const originalKeys=await request(issuer+'/protocol/openid-connect/certs');
    const originalClaims=validateIdToken(firstToken,originalKeys,options);
    let rotatedToken,newKid;
    assert.equal(interrupted,false);
    await withTemporarySigningKey(api,async(keys)=>{
      assert.equal(interrupted,false);
      newKid=keys.newKid;
      assert.equal(JSON.parse(Buffer.from(firstToken.split('.')[0],'base64url')).kid,keys.beforeKid);
      const overlap=await request(issuer+'/protocol/openid-connect/certs');
      assert.ok(overlap.keys.some(k=>k.kid===keys.beforeKid)&&overlap.keys.some(k=>k.kid===keys.newKid));
      validateIdToken(firstToken,overlap,options);
      await page.getByRole('button',{name:'Refresh stored session',exact:true}).click();
      await page.waitForFunction(()=>document.querySelectorAll('tbody tr').length===2);
      assert.equal(captured.length,2);rotatedToken=captured[1];
      assert.equal(JSON.parse(Buffer.from(rotatedToken.split('.')[0],'base64url')).kid,keys.newKid);
      const rotated=validateIdToken(rotatedToken,overlap,{...options,refresh:true});
      if(process.argv.includes('--delay-cleanup'))await new Promise(resolve=>setTimeout(resolve,65000));
      assert.equal(rotated.sub,originalClaims.sub);assert.equal(rotated.sid,originalClaims.sid);assert.equal(rotated.auth_time,originalClaims.auth_time);
    });
    if(process.argv.includes('--delay-cleanup'))assert.ok(managementGrants>=2);
    const restored=await request(issuer+'/protocol/openid-connect/certs');
    assert.ok(!restored.keys.some(k=>k.kid===newKid));
    assert.throws(()=>validateIdToken(rotatedToken,restored,{...options,refresh:true}));
    validateIdToken(firstToken,restored,options);
    await page.getByRole('button',{name:'Refresh stored session',exact:true}).click();
    await page.waitForFunction(()=>document.querySelectorAll('tbody tr').length===3);
    validateIdToken(captured.at(-1),restored,{...options,refresh:true});
    await page.getByRole('button',{name:'End provider session',exact:true}).click();
    await page.waitForFunction(()=>document.querySelectorAll('tbody tr').length===4);
    assert.match(await page.locator('tbody tr').last().innerText(),/logout.*true/s);
    console.log('Management authorization acquisitions: '+managementGrants+' (setup only, no browser-login proof).');
    console.log('PASS: actual Edge password/code/PKCE, original and rotated RS256 signatures, overlap JWKS, preserved refresh identity/session/auth_time, removed-key rejection, original active-key restoration, post-cleanup refresh and logout invalid_grant. Email ownership simulated; privileged access disabled.');
  }finally{
    process.removeListener('SIGINT',cancel);process.removeListener('SIGTERM',cancel);
    if(browser)await browser.close();
    if(server.listening)await new Promise(resolve=>server.close(resolve));
  }
}
run().catch(()=>{console.error('Local rotation investigation failed. No private details printed; inspect owned temporary-key cleanup status before rerunning.');process.exitCode=1});
