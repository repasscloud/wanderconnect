export const MAX_INPUT_BYTES = 10 * 1024 * 1024;
export const MAX_OUTPUT_BYTES = 20 * 1024 * 1024;
export const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export const HEX = /^[0-9a-f]{64}$/;
export const encoder = new TextEncoder();
export function jsonResponse(value,status=200,headers={}) { return Response.json(value,{status,headers:{'Cache-Control':'no-store',...headers}}); }
export class PipelineError extends Error { constructor(code,status=503,permanent=false){super(code);this.code=code;this.status=status;this.permanent=permanent;} }
export async function sha256(value) { return [...new Uint8Array(await crypto.subtle.digest('SHA-256',value))].map(x=>x.toString(16).padStart(2,'0')).join(''); }
const encode64 = bytes => btoa(String.fromCharCode(...bytes)).replace(/=/g,'').replace(/\+/g,'-').replace(/\//g,'_');
function decode64(value) {if(!/^[A-Za-z0-9_-]+$/.test(value)) throw new Error('invalid-base64');return Uint8Array.from(atob(value.replace(/-/g,'+').replace(/_/g,'/')+'='.repeat((4-value.length%4)%4)),c=>c.charCodeAt(0));}
async function key(secret,usage) { if(typeof secret!=='string'||secret.length<32)throw new PipelineError('missing-signing-key');return crypto.subtle.importKey('raw',encoder.encode(secret),{name:'HMAC',hash:'SHA-256'},false,[usage]); }
export async function signCapability(value,secret) { const payload=encode64(encoder.encode(JSON.stringify(value)));return `${payload}.${encode64(new Uint8Array(await crypto.subtle.sign('HMAC',await key(secret,'sign'),encoder.encode(payload))))}`; }
export async function verifyCapability(token,secret,now=Math.floor(Date.now()/1000)) {
 try {
  if(typeof token!=='string'||token.length>4096)throw new Error();
  const segments=token.split('.');if(segments.length!==2)throw new Error();
  if(!await crypto.subtle.verify('HMAC',await key(secret,'verify'),decode64(segments[1]),encoder.encode(segments[0])))throw new Error();
  const c=JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(decode64(segments[0])));
  if(c.protocolVersion!==1 || ![c.imageId,c.uploadId,c.jobId].every(v=>typeof v==='string'&&UUID.test(v)) || c.sourceKey!==`incoming/${c.imageId}/${c.uploadId}` || !['profile','album','chat'].includes(c.purpose) || !Number.isSafeInteger(c.maxBytes) || c.maxBytes<=0 || c.maxBytes>MAX_INPUT_BYTES || !Number.isSafeInteger(c.lifecycleRevision) || c.lifecycleRevision<1 || !Number.isSafeInteger(c.expiresAt) || c.expiresAt<=now || c.expiresAt>now+900 || c.sourceSha256!=null&&!HEX.test(c.sourceSha256))throw new Error();
  return c;
 } catch(e) { if(e instanceof PipelineError)throw e;throw new PipelineError('invalid-upload-permission',401,true); }
}
export async function readBounded(stream,limit) {
 if(!stream)throw new PipelineError('empty-body',400,true);
 const reader=stream.getReader();let size=0;const parts=[];
 try{while(true){const {done,value}=await reader.read();if(done)break;size+=value.byteLength;if(size>limit){await reader.cancel();throw new PipelineError('body-too-large',413,true);}parts.push(value);}}finally{reader.releaseLock();}
 const out=new Uint8Array(size);let offset=0;for(const part of parts){out.set(part,offset);offset+=part.byteLength;}return out;
}
export function secureBase(value,name,{local=false}={}) {let u;try{u=new URL(value);}catch{throw new PipelineError(`invalid-${name}`);}if(u.username||u.password||u.search||u.hash || u.protocol!=='https:'&&!(local&&u.protocol==='http:'&&['127.0.0.1','localhost'].includes(u.hostname)))throw new PipelineError(`invalid-${name}`);return u;}
export async function apiRequest(env,path,value,{fetcher=fetch}={}) {
 const base=secureBase(env.API_BASE_URL,'api-url',{local:env.ALLOW_LOCAL_HTTP==='true'});if(typeof env.API_SERVICE_TOKEN!=='string'||env.API_SERVICE_TOKEN.length<32)throw new PipelineError('missing-api-service-token');
 const response=await fetcher(new URL(path,base),{method:'POST',headers:{authorization:`Bearer ${env.API_SERVICE_TOKEN}`,'content-type':'application/json','Swoovr-Version':'2026-10-28'},body:JSON.stringify(value),signal:AbortSignal.timeout(15000),redirect:'error'});
 const bytes=await readBounded(response.body,128*1024);let body;try{body=JSON.parse(new TextDecoder().decode(bytes));}catch{throw new PipelineError('invalid-api-response');}
 if(!response.ok)throw new PipelineError(`api-${response.status}`,response.status,response.status>=400&&response.status<500&&![408,429].includes(response.status));
 return body;
}
export function errorResponse(error) {return jsonResponse({error:error instanceof PipelineError?error.code:'temporarily-unavailable'},error instanceof PipelineError?error.status:503);}
