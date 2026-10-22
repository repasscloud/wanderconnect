import {test} from 'node:test';
import assert from 'node:assert/strict';
import {withTemporaryOtp} from '../../scripts/auth/mfa.mjs';
const fixture={userId:'fixture-id',username:'fictional-adult',fictionalAdult:true,owner:'swoovr-local-investigation-v1'};
function fake(existing=[]){
 let credentials=[{id:'password',type:'password'},...existing],actions=[],mutations=0;
 const api=async(path,method='GET',body)=>{
  if(method!=='GET')mutations++;
  if(path==='/users/fixture-id'&&method==='GET')return {id:fixture.userId,username:fixture.username,requiredActions:actions};
  if(path==='/users/fixture-id'&&method==='PUT'){actions=body.requiredActions;return null}
  if(path==='/users/fixture-id/credentials')return structuredClone(credentials);
  if(path.startsWith('/users/fixture-id/credentials/')&&method==='DELETE'){credentials=credentials.filter(c=>c.id!==path.split('/').at(-1));return null}
  throw Error('Unexpected API request');
 };
 return {api,enroll:label=>{credentials.push({id:'new-otp',type:'otp',userLabel:label});actions=[]},credentials:()=>credentials,actions:()=>actions,mutations:()=>mutations};
}
test('optional OTP test removes only its own authenticator and preserves password',async()=>{
 const f=fake();await withTemporaryOtp(f.api,fixture,async({label})=>f.enroll(label));
 assert.deepEqual(f.credentials(),[{id:'password',type:'password'}]);assert.deepEqual(f.actions(),[]);
});
test('failed enrollment exercise still restores action and owned credential',async()=>{
 const f=fake();await assert.rejects(withTemporaryOtp(f.api,fixture,async({label})=>{f.enroll(label);throw Error('failed')}));
 assert.equal(f.credentials().length,1);assert.deepEqual(f.actions(),[]);
});
test('preexisting OTP is never modified',async()=>{
 const f=fake([{id:'existing',type:'otp',userLabel:'owner'}]);
 await assert.rejects(withTemporaryOtp(f.api,fixture,async()=>{}));assert.equal(f.mutations(),0);assert.equal(f.credentials().length,2);
});
test('failure before enrollment removes only the temporary required action',async()=>{
 const f=fake();await assert.rejects(withTemporaryOtp(f.api,fixture,async()=>{throw Error('before enrollment')}));
 assert.equal(f.credentials().length,1);assert.deepEqual(f.actions(),[]);
});
test('unexpected authenticator created during experiment is preserved and cleanup fails closed',async()=>{
 const f=fake();await assert.rejects(withTemporaryOtp(f.api,fixture,async()=>f.enroll('foreign-owner')));
 assert.ok(f.credentials().some(c=>c.id==='new-otp'&&c.userLabel==='foreign-owner'));
 assert.ok(f.credentials().some(c=>c.id==='password'));
});
test('fixed fixture user ID permits Keycloak lowercase username canonicalization',async()=>{
 const f=fake();await withTemporaryOtp(f.api,{...fixture,username:'Fictional-Adult'},async({label})=>f.enroll(label));
 assert.equal(f.credentials().length,1);
});
test('fixture username rename still rejects despite matching user ID',async()=>{
 const f=fake();await assert.rejects(withTemporaryOtp(f.api,{...fixture,username:'different-person'},async()=>{}));assert.equal(f.mutations(),0);
});
