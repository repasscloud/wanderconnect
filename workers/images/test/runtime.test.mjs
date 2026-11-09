import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { Miniflare, convertV4MiniflareOptions } from 'miniflare';
import { signCapability } from '../src/shared.mjs';
const secret='fictional-local-runtime-signing-key-32-chars';
test('real workerd runtime enforces HMAC and conditional R2 upload replay protection',async()=>{
 const mf=new Miniflare(convertV4MiniflareOptions({name:"upload",modules:[{type:'ESModule',path:'upload.mjs',contents:readFileSync(new URL('../src/upload.mjs',import.meta.url),'utf8')},{type:'ESModule',path:'shared.mjs',contents:readFileSync(new URL('../src/shared.mjs',import.meta.url),'utf8')}],compatibilityDate:'2026-11-08',r2Buckets:['QUARANTINE'],bindings:{UPLOAD_SIGNING_KEY:secret,ALLOWED_ORIGINS:'https://app.example.test'}}));
 try {
  const imageId='11111111-1111-4111-8111-111111111111',uploadId='22222222-2222-4222-8222-222222222222';
  const token=await signCapability({protocolVersion:1,imageId,uploadId,jobId:'33333333-3333-4333-8333-333333333333',sourceKey:`incoming/${imageId}/${uploadId}`,purpose:'profile',maxBytes:10485760,lifecycleRevision:1,expiresAt:Math.floor(Date.now()/1000)+300},secret);
  const send=()=>mf.dispatchFetch('https://upload.example.test/uploads',{method:'PUT',headers:{authorization:`Bearer ${token}`,'content-type':'image/png'},body:'synthetic bytes'});
  assert.equal((await send()).status,201);assert.equal((await send()).status,409);
  const bucket=await mf.getR2Bucket('QUARANTINE');const stored=await bucket.get(`incoming/${imageId}/${uploadId}`);assert.equal(await stored.text(),'synthetic bytes');
  assert.equal((await mf.dispatchFetch('https://upload.example.test/uploads',{method:'PUT',body:'bytes'})).status,401);
 }finally{await mf.dispose();}
});
