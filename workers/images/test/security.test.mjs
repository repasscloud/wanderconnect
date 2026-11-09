import test from 'node:test';
import assert from 'node:assert/strict';
import { signCapability, verifyCapability, readBounded, sha256, jsonResponse } from '../src/shared.mjs';
import upload from '../src/upload.mjs';
const secret='fictional-key-for-tests-only-at-least-32-chars';
const ids={imageId:'11111111-1111-4111-8111-111111111111',uploadId:'22222222-2222-4222-8222-222222222222',jobId:'33333333-3333-4333-8333-333333333333'};
const cap=()=>({protocolVersion:1,...ids,sourceKey:`incoming/${ids.imageId}/${ids.uploadId}`,purpose:'profile',maxBytes:10485760,lifecycleRevision:1,expiresAt:Math.floor(Date.now()/1000)+300});
test('capability rejects tampering, expiry, key manipulation and excessive scope',async()=>{
 let c=cap();let token=await signCapability(c,secret);assert.equal((await verifyCapability(token,secret)).imageId,c.imageId);
 await assert.rejects(()=>verifyCapability(token+'x',secret));
 for(const patch of [{expiresAt:1},{expiresAt:Math.floor(Date.now()/1000)+10000},{sourceKey:'incoming/other'},{maxBytes:10485761},{purpose:'evidence'}]){token=await signCapability({...c,...patch},secret);await assert.rejects(()=>verifyCapability(token,secret));}
});
test('bounded streams stop unadvertised oversized bodies',async()=>{await assert.rejects(()=>readBounded(new Response('oversize').body,3));assert.equal(new TextDecoder().decode(await readBounded(new Response('ok').body,3)),'ok');});
test('upload verifies capability and writes immutable exact object; replay cannot overwrite',async()=>{
 const rows=new Map();const bucket={put:async(k,v,o)=>{if(rows.has(k))return null; rows.set(k,{v,o}); return {etag:'e1'};}};
 const token=await signCapability(cap(),secret);const env={UPLOAD_SIGNING_KEY:secret,QUARANTINE:bucket,ALLOWED_ORIGINS:'https://app.example.test'};
 const req=()=>new Request('https://upload.example.test/uploads',{method:'PUT',headers:{authorization:`Bearer ${token}`,origin:'https://app.example.test','content-type':'image/png'},body:'abc'});
 assert.equal((await upload.fetch(req(),env)).status,201);assert.equal(rows.size,1);assert.equal((await upload.fetch(req(),env)).status,409);
 assert.equal((await upload.fetch(new Request('https://upload.example.test/uploads',{method:'PUT',body:'abc'}),env)).status,401);
 assert.equal(rows.values().next().value.o.customMetadata.sourceSha256,await sha256(new TextEncoder().encode('abc')));
});
