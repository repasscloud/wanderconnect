import { UUID,HEX,PipelineError,apiRequest,jsonResponse,errorResponse } from './shared.mjs';
export default {
 async fetch(request,env,context={}) {
  try {
   if(!['GET','HEAD'].includes(request.method))return jsonResponse({error:'method-not-allowed'},405,{Allow:'GET, HEAD'});
   const match=/^\/images\/([^/]+)\/(display|thumbnail)$/.exec(new URL(request.url).pathname);
   if(!match||!UUID.test(match[1]))return jsonResponse({error:'not-found'},404);
   const grant=request.headers.get('authorization');if(!grant?.startsWith('Bearer ')||grant.length>4096)return jsonResponse({error:'access-denied'},403);
   const result=await apiRequest(env,'/api/media/pipeline/access',{protocolVersion:1,imageId:match[1],variant:match[2],grant:grant.slice(7)},{fetcher:context.fetcher??fetch});
   if(result.allowed!==true)return jsonResponse({error:'access-denied'},403);
   if(typeof result.key!=='string'||!result.key.startsWith(`staged/${match[1]}/`)||result.contentType!=='image/webp'||!HEX.test(result.sha256??''))throw new PipelineError('invalid-access-decision');
   const object=request.method==='HEAD'?await env.PROCESSED.head(result.key):await env.PROCESSED.get(result.key);
   if(!object||object.customMetadata?.sha256!==result.sha256)return jsonResponse({error:'not-found'},404);
   return new Response(request.method==='HEAD'?null:object.body,{headers:{'Content-Type':'image/webp','Content-Length':String(object.size),'Cache-Control':'private, no-store','X-Content-Type-Options':'nosniff','Referrer-Policy':'no-referrer'}});
  }catch(error){return errorResponse(error);}
 }
};
