// Actual local BFF core smoke; admin-created fictional fixtures are NOT registration/provider proof.
const assert=require('node:assert/strict');
const {journeyCore}=require('../../scripts/uat/journey-core.cjs');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
const {web,createMember,memberToken,cli,matchReady,call}=require('../auth/local-tokens.cjs');
(async()=>{const edge=await require('../auth/managed-edge.cjs').managedEdge(chromium);let checks=0;
 try{
  const members=await Promise.all(['a','b'].map(x=>createMember('uat-core-'+x)));await cli('beta','add',...members.map(m=>m.email));const clients=[];
  for(const [i,member] of members.entries()){
   const token=await memberToken(edge.browser,member);assert.equal((await call(token,'/api/me/profile',{method:'PUT',body:{displayName:'Fictional UAT '+(i?'B':'A'),homeCityId:2078025}})).status,200);await matchReady(token);assert.equal((await call(token,'/api/me/modes/friends',{method:'PUT',body:{enabled:true,visible:true}})).status,200);
   const context=await edge.browser.newContext();const page=await context.newPage();await page.goto(web+'/account/sign-in');await page.fill('#username',member.email);await page.fill('#password',member.password);await page.click('#kc-login');await page.waitForURL(web+'/account');
   clients.push(async(path,{method='GET',body}={})=>{const response=await context.request.fetch(web+'/bff'+path,{method,headers:{'X-Swoovr-Bff':'1','Swoovr-Version':'2026-10-28'},...(body===undefined?{}:{data:body})});return {status:response.status(),body:response.status()===204?null:await response.json().catch(()=>null)};});
  }
  const result=await journeyCore(...clients,()=>checks++);assert.equal(result.steps,7);assert.equal(checks,7);
  console.log('PASS owned BFF core journey:7 stages (profile/session/discovery/request/accept/message/report+block/refusal). Local fictional admin-created accounts; phone gate off, no fresh registration/real email/SMS/Didit/UAT claim.');
 }finally{await edge.close();}
})().catch(()=>{console.error('Owned BFF core smoke failed; no private payload logged.');process.exitCode=1;});
