import { decide,POLICY_VERSION } from './policy.mjs';
import { MAX_INPUT_BYTES,MAX_OUTPUT_BYTES,UUID,HEX,PipelineError,readBounded,sha256,secureBase,apiRequest,jsonResponse } from './shared.mjs';
const resultKey = job => `results/${job.imageId}/${job.jobId}.json`;
function validateEvent(event,env) {
 if(!event || !['PutObject','CompleteMultipartUpload','CopyObject'].includes(event.action) || !env.QUARANTINE_BUCKET_NAME || event.bucket!==env.QUARANTINE_BUCKET_NAME || typeof event.object?.key!=='string' || !/^incoming\/[0-9a-f-]{36}\/[0-9a-f-]{36}$/i.test(event.object.key) || typeof event.object.eTag!=='string' || event.object.eTag.length<1 || event.object.eTag.length>128)throw new PipelineError('invalid-object-event',400,true);
}
function validateJob(job,event) {
 if(job.protocolVersion!==1||![job.imageId,job.jobId].every(v=>typeof v==='string'&&UUID.test(v))||job.sourceKey!==event.object.key||job.sourceEtag!==event.object.eTag||!['profile','album','chat'].includes(job.purpose)||!Number.isSafeInteger(job.lifecycleRevision)||job.lifecycleRevision<1||!Number.isSafeInteger(job.maxBytes)||job.maxBytes<=0||job.maxBytes>MAX_INPUT_BYTES||job.sourceSha256!=null&&!HEX.test(job.sourceSha256)||job.policyVersion&&job.policyVersion!==POLICY_VERSION)throw new PipelineError('invalid-processing-job',409,true);
}
export function validateManifest(manifest,job,digest,form) {
 if(manifest.protocolVersion!==1||manifest.jobId!==job.jobId||manifest.imageId!==job.imageId||manifest.sourceSha256!==digest||!Array.isArray(manifest.variants)||manifest.variants.length>2)throw new PipelineError('invalid-processor-manifest');
 const decision=decide(manifest.results);const expected=['passed','review'].includes(decision.outcome)&&decision.reason!=='known-content-test'?2:0;
 if(manifest.variants.length!==expected)throw new PipelineError('invalid-variant-count');
 const names=new Set();
 for(const item of manifest.variants) {
  if(!item||!['display','thumbnail'].includes(item.name)||names.has(item.name)||item.contentType!=='image/webp'||!Number.isSafeInteger(item.bytes)||item.bytes<12||item.bytes>MAX_OUTPUT_BYTES||!HEX.test(item.sha256)||![item.width,item.height].every(n=>Number.isSafeInteger(n)&&n>0)||Math.max(item.width,item.height)>(item.name==='thumbnail'?320:2048))throw new PipelineError('invalid-variant-manifest');
  names.add(item.name);
 }
 for(const [name]of form)if(name!=='manifest'&&!names.has(name))throw new PipelineError('unexpected-processor-part');
 if([...form.keys()].length!==expected+1)throw new PipelineError('duplicate-processor-part');
 return decision;
}
async function cachedReport(env,job) {
 const object=await env.PROCESSED.get(resultKey(job));if(!object)return null;
 const data=JSON.parse(new TextDecoder().decode(await readBounded(object.body,256*1024)));
 if(!HEX.test(data.sourceSha256??'')||job.sourceSha256&&job.sourceSha256!==data.sourceSha256||data.protocolVersion!==1||data.jobId!==job.jobId||data.imageId!==job.imageId||data.lifecycleRevision!==job.lifecycleRevision||data.sourceKey!==job.sourceKey||data.sourceEtag!==job.sourceEtag||data.policyVersion!==POLICY_VERSION||!UUID.test(data.eventId))throw new PipelineError('stale-result-manifest',409,true);
 const decision=decide(data.results);
 if(data.outcome!=='unsupported'&&(decision.outcome!==data.outcome||decision.reason!==data.reason))throw new PipelineError('invalid-cached-decision',409,true);
 if(!Array.isArray(data.variants)||data.variants.some(v=>v.key!==`staged/${job.imageId}/${job.jobId}/${data.eventId}/${v.name}.webp`||!HEX.test(v.sha256??'')))throw new PipelineError('invalid-cached-variants',409,true);
 return data;
}
async function saveReport(env,job,value,fetcher) {
 const committed=await env.PROCESSED.put(resultKey(job),JSON.stringify(value),{onlyIf:{etagDoesNotMatch:'*'},httpMetadata:{contentType:'application/json',cacheControl:'no-store'}});
 const saved=committed?value:await cachedReport(env,job);if(!saved)throw new PipelineError('result-manifest-unavailable');
 return report(env,job,saved,fetcher);
}
async function report(env,job,value,fetcher) {
 const response=await apiRequest(env,'/api/media/pipeline/results',value,{fetcher});
 if(response.accepted!==true)throw new PipelineError('outcome-not-accepted');
 if(value.outcome==='pending') {
  // The callback is durable. A fresh scan attempt is now allowed, retaining the raw source.
  await env.PROCESSED.delete(resultKey(job));
  throw new PipelineError('mandatory-check-pending');
 }
 return response;
}
function reportResults(results) {
 const states=new Set(['completed','failed','disabled','not_requested','unsupported']);
 const base=(r)=>({status:states.has(r?.status)?r.status:'unknown'});
 const r=results??{};
 const safeString=value=>typeof value==='string'&&value.length<=160?value:undefined;
 return {
  malware:{...base(r.malware),classification:safeString(r.malware?.classification),engineVersion:safeString(r.malware?.engineVersion),signatureVersion:safeString(r.malware?.signatureVersion),signatureTimestamp:safeString(r.malware?.signatureTimestamp),detection:safeString(r.malware?.detection)},
  nudenet:{...base(r.nudenet),modelVersion:safeString(r.nudenet?.modelVersion),modelSha256:safeString(r.nudenet?.modelSha256),detections:Array.isArray(r.nudenet?.detections)?r.nudenet.detections.slice(0,1000).map(d=>({class:safeString(d?.class),score:typeof d?.score==='number'&&Number.isFinite(d.score)?d.score:null})):undefined},
  nsfw:{...base(r.nsfw),modelVersion:safeString(r.nsfw?.modelVersion),modelRevision:safeString(r.nsfw?.modelRevision),is_nsfw:typeof r.nsfw?.is_nsfw==='boolean'?r.nsfw.is_nsfw:undefined},
  arachnid:{...base(r.arachnid),adapterVersion:safeString(r.arachnid?.adapterVersion),classification:safeString(r.arachnid?.classification),match_type:safeString(r.arachnid?.match_type)}
 };
}
export async function processEvent(event,env,{fetcher=fetch}={}) {
 validateEvent(event,env);
 const head=await env.QUARANTINE.head(event.object.key);
 const job=await apiRequest(env,'/api/media/pipeline/claim',{protocolVersion:1,sourceKey:event.object.key,sourceEtag:event.object.eTag,sourceUploadedAt:head?.uploaded?.toISOString(),sourceBytes:head?.size},{fetcher});
 if(job.action==='ignore'||job.action==='complete')return {ignored:true};
 if(job.action!=='process')throw new PipelineError('unknown-job-action');validateJob(job,event);
 let saved=await cachedReport(env,job);if(saved)return report(env,job,saved,fetcher);
 const source=await env.QUARANTINE.get(job.sourceKey,{onlyIf:{etagMatches:job.sourceEtag}});
 if(!source?.body||source.etag!==job.sourceEtag)throw new PipelineError('source-unavailable-or-changed');
 if(source.size>job.maxBytes)throw new PipelineError('source-too-large',413,true);
 const original=await readBounded(source.body,job.maxBytes);if(!original.length)throw new PipelineError('empty-source',400,true);
 const digest=await sha256(original);if(job.sourceSha256&&job.sourceSha256!==digest)throw new PipelineError('source-digest-mismatch',409,true);
 if(source.customMetadata?.sourceSha256&&source.customMetadata.sourceSha256!==digest)throw new PipelineError('stored-digest-mismatch',409,true);
 const processor=secureBase(env.PROCESSOR_BASE_URL,'processor-url',{local:env.ALLOW_LOCAL_HTTP==='true'});
 if(typeof env.PROCESSOR_API_KEY!=='string'||env.PROCESSOR_API_KEY.length<32)throw new PipelineError('missing-processor-key');
 const input=new FormData();input.append('job',JSON.stringify({protocolVersion:1,jobId:job.jobId,imageId:job.imageId,sourceSha256:digest,purpose:job.purpose}));input.append('file',new Blob([original],{type:'application/octet-stream'}),'upload');
 const response=await fetcher(new URL('/v1/process',processor),{method:'POST',headers:{authorization:`Bearer ${env.PROCESSOR_API_KEY}`},body:input,signal:AbortSignal.timeout(180000),redirect:'error'});
 if([400,413,415,422].includes(response.status)) {
  // Authenticated processor permanently refused these bytes. Preserve a durable non-deliverable result.
  await response.body?.cancel();
  return saveReport(env,job,{protocolVersion:1,eventId:crypto.randomUUID(),jobId:job.jobId,imageId:job.imageId,sourceKey:job.sourceKey,sourceEtag:job.sourceEtag,sourceSha256:digest,lifecycleRevision:job.lifecycleRevision,policyVersion:POLICY_VERSION,outcome:'unsupported',reason:'unsupported-image',results:{},variants:[]},fetcher);
 }
 if(!response.ok)throw new PipelineError(`processor-${response.status}`);
 if(!response.headers.get('content-type')?.toLowerCase().startsWith('multipart/form-data;'))throw new PipelineError('invalid-processor-content-type');
 const bytes=await readBounded(response.body,MAX_OUTPUT_BYTES+256*1024);
 const form=await new Response(bytes,{headers:{'content-type':response.headers.get('content-type')}}).formData();
 const raw=form.get('manifest');if(typeof raw!=='string'||raw.length>128*1024)throw new PipelineError('invalid-processor-manifest');
 let manifest;try{manifest=JSON.parse(raw);}catch{throw new PipelineError('invalid-processor-json');}
 const decision=validateManifest(manifest,job,digest,form);const eventId=crypto.randomUUID();const variants=[];
 // Validate every byte before writing any output, so a malformed sibling cannot leave a valid-looking result.
 const ready=[];let total=0;
 for(const item of manifest.variants) {
  const part=form.get(item.name);if(typeof part==='string'||!part||part.size!==item.bytes||part.type!=='image/webp')throw new PipelineError('invalid-variant-part');
  const data=new Uint8Array(await part.arrayBuffer());total+=data.length;
  if(total>MAX_OUTPUT_BYTES||await sha256(data)!==item.sha256||String.fromCharCode(...data.slice(0,4))!=='RIFF'||String.fromCharCode(...data.slice(8,12))!=='WEBP')throw new PipelineError('invalid-variant-bytes');
  ready.push({item,data});
 }
 for(const {item,data} of ready) {
  const key=`staged/${job.imageId}/${job.jobId}/${eventId}/${item.name}.webp`;
  const put=await env.PROCESSED.put(key,data,{onlyIf:{etagDoesNotMatch:'*'},httpMetadata:{contentType:'image/webp',cacheControl:'private, no-store'},customMetadata:{imageId:job.imageId,jobId:job.jobId,sha256:item.sha256}});
  if(!put)throw new PipelineError('variant-write-conflict');variants.push({...item,key});
 }
 const value={protocolVersion:1,eventId,jobId:job.jobId,imageId:job.imageId,sourceKey:job.sourceKey,sourceEtag:job.sourceEtag,sourceSha256:digest,lifecycleRevision:job.lifecycleRevision,...decision,results:reportResults(manifest.results),variants};
 return saveReport(env,job,value,fetcher);
}
export default {
 async queue(batch,env) {
  // Deliberately sequential within a batch. Queue max_concurrency is bounded to measured VM capacity.
  for(const message of batch.messages)try{await processEvent(message.body,env);message.ack();}catch(error){
   console.warn(JSON.stringify({event:'image-job-failed',code:error instanceof PipelineError?error.code:'unexpected-error',attempt:message.attempts??0}));
   // Even permanent payload/auth failures reach DLQ rather than being silently discarded.
   message.retry({delaySeconds:Math.min(3600,30*2**Math.min(message.attempts??1,7))});
  }
 },
 async fetch(){return jsonResponse({service:'swoovr-image-processing',mode:'queue-only'},404);}
};
