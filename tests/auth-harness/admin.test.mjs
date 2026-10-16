import { test } from 'node:test';
import assert from 'node:assert/strict';
import { localRealmApi } from '../../scripts/auth/provision.mjs';

test('local admin credentials renew before expiry during delayed cleanup work',async()=>{
 let now=0,grants=0;
 const provider=async(url,options)=>{
  if(url.includes('/realms/master/'))return {access_token:'synthetic-'+(++grants),expires_in:60};
  assert.equal(options.headers.Authorization,'Bearer synthetic-'+grants);
  return [];
 };
 const api=await localRealmApi({provider,read:async()=> 'KC_BOOTSTRAP_ADMIN_PASSWORD=synthetic-test-only',clock:()=>now});
 await api('/components');now=61000;await api('/components');
 assert.equal(grants,2);
});
test('local admin retries once after a provider 401 and never treats it as cleanup success',async()=>{
 let grants=0,calls=0;
 const provider=async(url)=>{
  if(url.includes('/realms/master/'))return {access_token:'synthetic-'+(++grants),expires_in:60};
  calls++;if(calls===1)throw Object.assign(Error('synthetic rejection'),{status:401});return [];
 };
 const api=await localRealmApi({provider,read:async()=> 'KC_BOOTSTRAP_ADMIN_PASSWORD=synthetic-test-only',clock:()=>0});
 await api('/components');assert.equal(grants,2);assert.equal(calls,2);
});
