// Actual owned API capacity under concurrent requests. Verification states are synthetic SQL fixtures, not Didit evidence.
const assert=require('node:assert/strict');
if(!process.env.SWOOVR_TEST_APP_DATABASE||process.env.Founders__Places!=='1')throw Error('Use owned one-place founder runner.');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright'),{managedEdge}=require('../auth/managed-edge.cjs');
const {call,createMember,memberToken,rootAdminToken,cli,sql,claims,mailTo}=require('../auth/local-tokens.cjs');
(async()=>{
 const edge=await managedEdge(chromium);let checks=0;
 try{
  const root=await rootAdminToken(edge.browser),members=await Promise.all(['first','second','third'].map(name=>createMember('founder-cap-'+name)));
  await cli('beta','add',...members.map(m=>m.email));
  const ids=[];
  for(const member of members){const token=await memberToken(edge.browser,member);assert.equal((await call(token,'/api/me/profile',{method:'PUT',body:{displayName:'Fictional Founder',homeCityId:2078025}})).status,200);ids.push(await sql(`SELECT id FROM accounts WHERE subject='${claims(token).sub}'`));}
  assert.equal(await sql('SELECT count(*) FROM founder_places'),'0');checks++;
  assert.equal((await call(root,`/api/admin/accounts/${ids[0]}/founder`,{method:'POST'})).status,409);checks++;
  await sql(`UPDATE member_profiles SET id_status='Verified' WHERE account_id IN (${ids.map(id=>`'${id}'`).join(',')})`);
  const results=await Promise.all(ids.slice(0,2).map(id=>call(root,`/api/admin/accounts/${id}/founder`,{method:'POST'})));
  assert.deepEqual(results.map(r=>r.status).sort(),[201,409]);checks++;
  const winner=results.findIndex(r=>r.status===201),loser=1-winner;
  assert.equal(results[loser].body.code,'founder-places-full');assert.equal(results[winner].body.place,1);checks++;
  assert.equal(await sql('SELECT count(*) FROM founder_places'),'1');assert.equal(await sql('SELECT count(*) FROM founder_tokens'), '1');checks++;
  assert.equal(await sql('SELECT count(*) FROM member_profiles WHERE founding_member=true'),'1');checks++;
  const full=await call(root,`/api/admin/accounts/${ids[2]}/founder`,{method:'POST'});assert.deepEqual([full.status,full.body.code],[409,'founder-places-full']);checks++;
  const duplicate=await call(root,`/api/admin/accounts/${ids[winner]}/founder`,{method:'POST'});assert.deepEqual([duplicate.status,duplicate.body.code],[409,'already-founder']);checks++;
  const before=await sql(`SELECT founding_since::text||'|'||founder_entitlement_from::text FROM member_profiles WHERE account_id='${ids[winner]}'`);
  assert.equal((await mailTo(members[winner].email)).length,1);assert.equal((await mailTo(members[loser].email)).length,0);checks++;
  assert.equal((await call(root,`/api/admin/accounts/${ids[winner]}/founder/resend`,{method:'POST'})).status,200);checks++;
  assert.equal((await mailTo(members[winner].email)).length,2);assert.equal(await sql('SELECT count(*) FROM founder_tokens'),'1');checks++;
  assert.equal(await sql(`SELECT founding_since::text||'|'||founder_entitlement_from::text FROM member_profiles WHERE account_id='${ids[winner]}'`),before,'resend never restarts founder timing');checks++;
  console.log(`PASS founder actual API capacity/concurrency: ${checks} checks (synthetic eligibility; local Mailpit only; no provider/payment/delivery evidence).`);
 }finally{await edge.close();}
})().catch(error=>{console.error(error);process.exitCode=1});
