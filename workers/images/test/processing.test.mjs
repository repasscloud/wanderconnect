import test from 'node:test';
import assert from 'node:assert/strict';
import { processEvent } from '../src/processing.mjs';
import delivery from '../src/delivery.mjs';
import { sha256 } from '../src/shared.mjs';
const ids={imageId:'11111111-1111-4111-8111-111111111111',jobId:'33333333-3333-4333-8333-333333333333'};
const sourceKey=`incoming/${ids.imageId}/22222222-2222-4222-8222-222222222222`;
const source=new TextEncoder().encode('fictional original');const webp=new Uint8Array([82,73,70,70,4,0,0,0,87,69,66,80]);
const clean=()=>({malware:{status:'completed',classification:'clean',engineVersion:'test',signatureVersion:'test'},nudenet:{status:'completed',detections:[]},nsfw:{status:'completed',is_nsfw:false},arachnid:{status:'completed',classification:'no-known-match'}});
class Bucket{constructor(){this.rows=new Map();}async put(k,v,o={}){if(o.onlyIf&&this.rows.has(k))return null;this.rows.set(k,{v,customMetadata:o.customMetadata??{},size:v.length??v.byteLength});return {etag:'stored'};}async head(k){const r=this.rows.get(k);return r?{...r,etag:"etag1",uploaded:new Date()}:null;}async get(k){const r=this.rows.get(k);if(!r)return null;return {...r,etag:'etag1',body:new Response(r.v).body};}async delete(k){this.rows.delete(k);}}
async function fixture(){const quarantine=new Bucket(),processed=new Bucket();await quarantine.put(sourceKey,source);const digest=await sha256(source);const job={protocolVersion:1,...ids,sourceKey,sourceEtag:'etag1',sourceSha256:digest,purpose:'profile',maxBytes:10485760,lifecycleRevision:1,action:'process'};let scans=0,reports=0;const state={failReport:false,job,reported:[]};const fetcher=async(url,options)=>{
 const u=new URL(url);
 if(u.pathname.endsWith('/claim'))return Response.json(state.job);
 if(u.pathname.endsWith('/results')){reports++;state.reported.push(JSON.parse(options.body));if(state.failReport)return Response.json({error:'unavailable'},{status:503});return Response.json({accepted:true,deliverable:false});}
 if(u.pathname==='/v1/process'){const scanNumber=++scans;if(state.processorStatus)return Response.json({error:{code:'invalid_image'}},{status:state.processorStatus});const f=new FormData();const manifest={protocolVersion:1,...ids,sourceSha256:digest,results:clean(),variants:[]};for(const name of ['display','thumbnail']){manifest.variants.push({name,contentType:'image/webp',width:32,height:32,bytes:webp.length,sha256:await sha256(webp)});f.append(name,new Blob([webp],{type:'image/webp'}),name+'.webp');}if(state.mutate)state.mutate(manifest,scanNumber);if(manifest.variants.length===0){f.delete('display');f.delete('thumbnail');}f.append('manifest',JSON.stringify(manifest));return new Response(f);}
 throw new Error('unexpected '+url);
};return {env:{QUARANTINE:quarantine,PROCESSED:processed,API_BASE_URL:'https://api.example.test',API_SERVICE_TOKEN:'fictional-service-token-at-least-32-chars',PROCESSOR_BASE_URL:'https://processor.example.test',PROCESSOR_API_KEY:'fictional-processor-key-at-least-32-chars',QUARANTINE_BUCKET_NAME:'quarantine-example'},fetcher,state,event:{action:'PutObject',bucket:'quarantine-example',object:{key:sourceKey,eTag:'etag1'}},counts:()=>({scans,reports})};}
test('one processing visit stores private variants and reports; report retry reuses durable result',async()=>{const f=await fixture();f.state.failReport=true;await assert.rejects(()=>processEvent(f.event,f.env,{fetcher:f.fetcher}));assert.equal(f.counts().scans,1);f.state.failReport=false;await processEvent(f.event,f.env,{fetcher:f.fetcher});assert.equal(f.counts().scans,1);assert.equal(f.counts().reports,2);assert.equal(f.env.PROCESSED.rows.size,3);});
test('forged/recursive event and stale claim cannot invoke processing',async()=>{const f=await fixture();await assert.rejects(()=>processEvent({...f.event,bucket:'approved'},f.env,{fetcher:f.fetcher}));f.state.job={...f.state.job,sourceKey:'incoming/wrong'};await assert.rejects(()=>processEvent(f.event,f.env,{fetcher:f.fetcher}));assert.equal(f.counts().scans,0);});
test('malformed processor identity or variant digest is never stored/reported as success',async()=>{for(const mutate of [m=>m.jobId='wrong',m=>m.variants[0].sha256='0'.repeat(64),m=>m.variants[0].name='../bad']){const f=await fixture();f.state.mutate=mutate;await assert.rejects(()=>processEvent(f.event,f.env,{fetcher:f.fetcher}));assert.equal(f.counts().reports,0);assert.equal(f.env.PROCESSED.rows.size,0);}});
test('delivery denies without API permission for GET and HEAD',async()=>{for(const method of ['GET','HEAD']){let reads=0;const env={API_BASE_URL:'https://api.example.test',API_SERVICE_TOKEN:'fictional-service-token-at-least-32-chars',PROCESSED:{get(){reads++;}}};const request=new Request(`https://images.example.test/images/${ids.imageId}/display`,{method,headers:{authorization:'Bearer fictional-grant'}});const response=await delivery.fetch(request,env,{fetcher:async()=>Response.json({allowed:false})});assert.equal(response.status,403);assert.equal(reads,0);}});

test('provider-test result reports review without variants and permanent invalid image reports unsupported',async()=>{const f=await fixture();f.state.mutate=m=>{m.results.arachnid.classification='test';m.variants=[];};await processEvent(f.event,f.env,{fetcher:f.fetcher});assert.equal(f.counts().reports,1);assert.equal(f.env.PROCESSED.rows.size,1);for(const status of [400,413,415,422]){const f=await fixture();f.state.processorStatus=status;await processEvent(f.event,f.env,{fetcher:f.fetcher});assert.equal(f.counts().reports,1);const report=JSON.parse(f.env.PROCESSED.rows.values().next().value.v);assert.equal(report.outcome,'unsupported');}});

test('parallel duplicate attempts converge on one durable event identity',async()=>{const f=await fixture();await Promise.all([processEvent(f.event,f.env,{fetcher:f.fetcher}),processEvent(f.event,f.env,{fetcher:f.fetcher})]);assert.equal(f.state.reported.length,2);assert.equal(f.state.reported[0].eventId,f.state.reported[1].eventId);});
test('raw provider payloads are not persisted or sent to API',async()=>{const f=await fixture();f.state.mutate=m=>{m.results.arachnid.raw={sensitive:'do-not-store'};m.results.arachnid.pdq='private-fingerprint';};await processEvent(f.event,f.env,{fetcher:f.fetcher});assert.equal(JSON.stringify(f.state.reported).includes('do-not-store'),false);assert.equal(JSON.stringify([...f.env.PROCESSED.rows.values()]).includes('private-fingerprint'),false);});
test('pending mandatory check retries after durable report; does not become permanently cached',async()=>{const f=await fixture();f.state.mutate=m=>{m.results.arachnid.status='failed';m.variants=[];};await assert.rejects(()=>processEvent(f.event,f.env,{fetcher:f.fetcher}));assert.equal(f.env.PROCESSED.rows.size,0);f.state.mutate=undefined;await processEvent(f.event,f.env,{fetcher:f.fetcher});assert.equal(f.counts().scans,2);assert.equal(f.state.reported[0].outcome,'pending');assert.equal(f.state.reported[1].outcome,'passed');});

test('model and fingerprint-adapter versions survive sanitised durable reporting',async()=>{
 const f=await fixture();f.state.mutate=m=>{m.results.nudenet.modelVersion='nudenet-3.4.2-320n';m.results.nsfw.modelVersion='nsfw-image-detector-0.1.2';m.results.nsfw.modelRevision='a'.repeat(40);m.results.arachnid.adapterVersion='pdqhash-0.2.8';};
 await processEvent(f.event,f.env,{fetcher:f.fetcher});const results=f.state.reported[0].results;
 assert.equal(results.nudenet.modelVersion,'nudenet-3.4.2-320n');assert.equal(results.nsfw.modelRevision,'a'.repeat(40));assert.equal(results.arachnid.adapterVersion,'pdqhash-0.2.8');
});

async function specialistRace({failBlock=false}={}) {
 const f=await fixture();let releaseReview;const reviewCommitted=new Promise(resolve=>{releaseReview=resolve;});let processorCalls=0;const callbacks=[];
 f.state.mutate=(m,scan)=>{m.results.arachnid.classification=scan===1?'test':'csam';m.variants=[];};
 const fetcher=async(url,options)=>{
  const path=new URL(url).pathname;
  if(path==='/v1/process'&&++processorCalls===2)await reviewCommitted;
  if(path.endsWith('/results')) {
   const value=JSON.parse(options.body);callbacks.push(value);
   if(value.outcome==='blocked'&&failBlock)return Response.json({error:'temporary'},{status:503});
   const response=await f.fetcher(url,options);if(value.outcome==='review')releaseReview();return response;
  }
  return f.fetcher(url,options);
 };
 const settled=await Promise.allSettled([processEvent(f.event,f.env,{fetcher}),processEvent(f.event,f.env,{fetcher})]);
 return {f,fetcher,callbacks,settled};
}

test('specialist evidence survives losing an overlapping non-specialist manifest race',{timeout:5000},async()=>{
 const {f,callbacks,settled}=await specialistRace();assert.ok(settled.every(r=>r.status==='fulfilled'));
 assert.ok(callbacks.some(r=>r.outcome==='review'));assert.ok(callbacks.some(r=>r.outcome==='blocked'));
 const records=[...f.env.PROCESSED.rows.values()].map(row=>{try{return JSON.parse(row.v);}catch{return null;}});
 assert.ok(records.some(r=>r?.outcome==='blocked'));
});

test('durable specialist callback retries even when the next claim would complete or ignore',{timeout:5000},async()=>{
 for(const action of ['complete','ignore']) {
  const {f,callbacks,settled}=await specialistRace({failBlock:true});assert.ok(settled.some(r=>r.status==='rejected'));
  f.state.job={action};const scans=f.counts().scans;
  const retryFetcher=async(url,options)=>{if(new URL(url).pathname.endsWith('/results')){callbacks.push(JSON.parse(options.body));return Response.json({accepted:true,deliverable:false});}return f.fetcher(url,options);};
  await processEvent(f.event,f.env,{fetcher:retryFetcher});assert.equal(callbacks.at(-1).outcome,'blocked');assert.equal(f.counts().scans,scans);
 }
});
