import {test} from 'node:test';
import assert from 'node:assert/strict';
import {totp} from '../../scripts/auth/totp.mjs';
const secret='GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ';
for(const [time,expected] of [[59,'94287082'],[1111111109,'07081804'],[1111111111,'14050471'],[1234567890,'89005924'],[2000000000,'69279037'],[20000000000,'65353130']])
 test('RFC6238 SHA1 vector at '+time,()=>assert.equal(totp(secret,time,8),expected));
test('OTP secret/time/width validation',()=>{
 for(const value of ['', 'bad!', '12345'])assert.throws(()=>totp(value,59));
 assert.throws(()=>totp(secret,-1));assert.throws(()=>totp(secret,59,0));
 assert.equal(totp(secret,59),'287082');
});
