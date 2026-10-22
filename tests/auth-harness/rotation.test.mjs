import { test } from 'node:test';
import assert from 'node:assert/strict';
import { withTemporarySigningKey } from '../../scripts/auth/rotation.mjs';
function fake() {
  let component;
  let deleted=0;
  const api=async (path,method='GET',body) => {
    if(path==='') return {realm:'swoovr-dev',id:'realm-id'};
    if(path==='/components' && method==='GET') return component?[component]:[];
    if(path==='/keys') return {active:{RS256:component?'new-key':'old-key'}};
    if(path==='/components' && method==='POST') {component={...body,id:'temporary'};return null;}
    if(path==='/components/temporary' && method==='DELETE') {component=undefined;deleted++;return null;}
    throw Error('Unexpected request');
  };
  return {api,deleted:()=>deleted};
}
test('temporary signing key restores original active key after success',async()=>{
 const f=fake();let observed=false;
 await withTemporarySigningKey(f.api,async({beforeKid,newKid})=>{assert.equal(beforeKid,'old-key');assert.equal(newKid,'new-key');observed=true});
 assert.equal(observed,true);assert.equal(f.deleted(),1);
});
test('temporary signing key cleans up when browser exercise fails',async()=>{
 const f=fake();await assert.rejects(withTemporarySigningKey(f.api,async()=>{throw Error('exercise failed')}));assert.equal(f.deleted(),1);
});
test('existing investigation rotation refuses to mutate or delete it',async()=>{
 let mutated=false;
 const api=async(path,method='GET')=>{
  if(method!=='GET'){mutated=true;throw Error('No mutation permitted')}
  if(path==='')return {realm:'swoovr-dev',id:'realm-id'};
  if(path==='/components')return [{name:'swoovr-investigation-rotation-existing'}];
  if(path==='/keys')return {active:{RS256:'old-key'}};
 };
 await assert.rejects(withTemporarySigningKey(api,async()=>{}));assert.equal(mutated,false);
});
test('lost create response still discovers and removes its own temporary provider',async()=>{
 const f=fake();
 const api=async(path,method='GET',body)=>{
  const value=await f.api(path,method,body);
  if(path==='/components' && method==='POST')throw Error('response lost after commit');
  return value;
 };
 await assert.rejects(withTemporarySigningKey(api,async()=>{throw Error('must not exercise')}));
 assert.equal(f.deleted(),1);
});
test('cleanup reacquires expired management authorization after delayed exercise',async()=>{
 const {localRealmApi}=await import('../../scripts/auth/provision.mjs');
 const f=fake();let now=0,grants=0;
 const provider=async(url,options)=>{
  if(url.includes('/realms/master/'))return {access_token:'synthetic-'+(++grants),expires_in:60};
  if(now>=60000 && grants<2)throw Object.assign(Error('expired'),{status:401});
  return f.api(new URL(url).pathname.slice('/admin/realms/swoovr-dev'.length),options.method,options.body?JSON.parse(options.body):undefined);
 };
 const api=await localRealmApi({provider,read:async()=> 'KC_BOOTSTRAP_ADMIN_PASSWORD=synthetic-test-only',clock:()=>now});
 await withTemporarySigningKey(api,async()=>{now=61000});
 assert.equal(f.deleted(),1);assert.equal(grants,2);
});
