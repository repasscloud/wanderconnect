import { verifyCapability,readBounded,sha256,jsonResponse,errorResponse,PipelineError } from './shared.mjs';
function originHeaders(request,env){const origin=request.headers.get('origin');if(!origin)return {};const allowed=(env.ALLOWED_ORIGINS??'').split(',').map(s=>s.trim());if(!allowed.includes(origin))throw new PipelineError('origin-not-allowed',403,true);return {'Access-Control-Allow-Origin':origin,'Vary':'Origin','Access-Control-Allow-Methods':'PUT, OPTIONS','Access-Control-Allow-Headers':'Authorization, Content-Type','Access-Control-Max-Age':'300'};}
export default {
 async fetch(request,env) {
  let cors={};try{
   const url=new URL(request.url);if(url.pathname!=='/uploads')return jsonResponse({error:'not-found'},404);
   cors=originHeaders(request,env);if(request.method==='OPTIONS')return new Response(null,{status:204,headers:cors});
   if(request.method!=='PUT')return jsonResponse({error:'method-not-allowed'},405,{Allow:'PUT, OPTIONS',...cors});
   const auth=request.headers.get('authorization');if(!auth?.startsWith('Bearer '))throw new PipelineError('invalid-upload-permission',401,true);
   const c=await verifyCapability(auth.slice(7),env.UPLOAD_SIGNING_KEY);
   if(!['image/jpeg','image/png','image/webp'].includes(request.headers.get('content-type')?.split(';')[0].trim()))throw new PipelineError('unsupported-content-type',415,true);
   const declared=request.headers.get('content-length');if(declared!==null&&(!/^\d+$/.test(declared)||Number(declared)>c.maxBytes))throw new PipelineError('body-too-large',413,true);
   const data=await readBounded(request.body,c.maxBytes);if(data.length===0)throw new PipelineError('empty-body',400,true);
   const digest=await sha256(data);if(c.sourceSha256&&c.sourceSha256!==digest)throw new PipelineError('source-digest-mismatch',400,true);
   const saved=await env.QUARANTINE.put(c.sourceKey,data,{onlyIf:{etagDoesNotMatch:'*'},httpMetadata:{contentType:'application/octet-stream',cacheControl:'no-store'},customMetadata:{imageId:c.imageId,jobId:c.jobId,uploadId:c.uploadId,purpose:c.purpose,lifecycleRevision:String(c.lifecycleRevision),sourceSha256:digest}});
   if(!saved)throw new PipelineError('upload-already-stored',409,true);
   return jsonResponse({imageId:c.imageId,status:'pending'},201,cors);
  }catch(error){const response=errorResponse(error);for(const [k,v]of Object.entries(cors))response.headers.set(k,v);return response;}
 }
};
