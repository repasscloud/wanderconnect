// Actual compiled SSR/header contract in Development and Uat, without Keycloak/provider requests.
const {test}=require('node:test'),assert=require('node:assert/strict'),crypto=require('node:crypto');
const http=require('node:http');
const {spawn}=require('node:child_process'),net=require('node:net'),path=require('node:path'),{once}=require('node:events');
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
function get(url){return new Promise((resolve,reject)=>{
 const request=http.get(url,{agent:false,headers:{Host:'uat.swoovr.app','X-Forwarded-Proto':'https'}},response=>{
  let body='';response.setEncoding('utf8');response.on('data',chunk=>{body+=chunk;if(body.length>4*1024*1024)request.destroy(new Error('Owned SSR response exceeded bound.'));});response.on('end',()=>resolve({status:response.statusCode,headers:response.headers,body}));response.on('error',reject);
 });request.setTimeout(1000,()=>request.destroy(new Error('Owned SSR request timeout.')));request.on('error',reject);
});}
for(const environment of ['Development','Uat'])test(environment+' authorizes exactly the rendered Blazor import map',{timeout:45000},async()=>{
 const reserve=net.createServer();await new Promise((r,j)=>{reserve.once('error',j);reserve.listen(0,'127.0.0.1',r);});const port=reserve.address().port;await new Promise(r=>reserve.close(r));
 const cwd=path.resolve('src/Swoovr.Web'),dll=path.join(cwd,'bin/Release/net10.0/Swoovr.Web.dll');
 const child=spawn('dotnet',[dll],{cwd,env:{...process.env,DOTNET_ENVIRONMENT:environment,ASPNETCORE_ENVIRONMENT:environment,ASPNETCORE_URLS:'http://127.0.0.1:'+port,Hosting__PublicOrigin:'https://uat.swoovr.app',AllowedHosts:'uat.swoovr.app',Hosting__TrustedProxyNetworks:'127.0.0.1/32',Staff__LocalPasswords:'false',Prototype__Enabled:'false',Keycloak__RequireHttpsMetadata:'true',Keycloak__Authority:'https://auth.uat.swoovr.app/realms/swoovr-members',Keycloak__ClientId:'swoovr-web',Keycloak__ClientSecret:'fictional-private-host-csp-check',Api__BaseUrl:'https://api.example.test/','Logging__LogLevel__Default':'Warning'},stdio:'ignore'});
 try{
  let response;const deadline=Date.now()+15000;
  while(Date.now()<deadline){
   assert.equal(child.exitCode,null,'owned host must stay running');
   try{response=await get('http://127.0.0.1:'+port+'/account');if(response.status===200)break;}catch{}
   await sleep(100);
  }
  assert.equal(response?.status,200);const html=response.body;const map=html.match(/<script\b[^>]*type="importmap"[^>]*>([\s\S]*?)<\/script>/i);assert.ok(map);
  const integrity='sha256-'+crypto.createHash('sha256').update(map[1].replace(/\r\n?/g,'\n')).digest('base64');
  const script=(response.headers['content-security-policy']??'').split(';').find(v=>v.trim().startsWith('script-src '));
  assert.ok(script?.includes("'"+integrity+"'"),'policy must authorize the exact rendered bytes');assert.equal(map[0].match(/integrity="([^"]+)"/)[1].replace(/&#x([0-9a-f]+);/gi,(_,hex)=>String.fromCodePoint(parseInt(hex,16))),integrity);
  assert.ok(!script.includes("'unsafe-inline'")&&!script.includes("'unsafe-eval'"));
  // Stable asset metadata must remain authorized across documents/enhanced navigation, without a broad nonce.
  const next=await get('http://127.0.0.1:'+port+'/privacy');
  assert.equal(next.status,200);assert.equal(next.headers['content-security-policy'],response.headers['content-security-policy']);
 }finally{
  if(child.exitCode===null&&child.signalCode===null){const stopped=once(child,'exit');child.kill('SIGTERM');await Promise.race([stopped,sleep(5000)]);if(child.exitCode===null&&child.signalCode===null){child.kill('SIGKILL');await stopped;}}
 }
});
