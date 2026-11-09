// D355: sibling auth host uses the unchanged member RP via an exact related-origin declaration.
const {test}=require('node:test'),assert=require('node:assert/strict');
const http=require('node:http'),net=require('node:net'),path=require('node:path');
const {spawn}=require('node:child_process'),{once}=require('node:events');
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
function get(port,host,url){return new Promise((resolve,reject)=>{const req=http.get({hostname:'127.0.0.1',port,path:url,headers:{Host:host,'X-Forwarded-Proto':'https',Origin:'https://untrusted.example.test'}},res=>{let body='';res.setEncoding('utf8');res.on('data',chunk=>body+=chunk);res.on('end',()=>resolve({status:res.statusCode,headers:res.headers,body}));});req.on('error',reject);req.setTimeout(1500,()=>req.destroy(new Error('Owned HTTP test timeout')));});}
for(const [host,auth,allowed]of [['uat.swoovr.app','auth-uat.swoovr.app',true],['swoovr.app','auth.swoovr.app',false],['uat.swoovr.app','auth.swoovr.app',false],['swoovr.app','auth-uat.swoovr.app',false]])test('WebAuthn related origins are isolated for '+host,{timeout:45000},async()=>{
 const reserve=net.createServer();await new Promise(r=>reserve.listen(0,'127.0.0.1',r));const port=reserve.address().port;await new Promise(r=>reserve.close(r));
 const cwd=path.resolve('src/Swoovr.Web');const child=spawn('dotnet',[path.join(cwd,'bin/Release/net10.0/Swoovr.Web.dll')],{cwd,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Uat',ASPNETCORE_URLS:'http://127.0.0.1:'+port,Hosting__PublicOrigin:'https://'+host,AllowedHosts:host,Hosting__TrustedProxyNetworks:'127.0.0.1/32',Staff__LocalPasswords:'false',Prototype__Enabled:'false',Keycloak__RequireHttpsMetadata:'true',Keycloak__Authority:'https://'+auth+'/realms/swoovr-members',Keycloak__ClientId:'swoovr-web',Keycloak__ClientSecret:'fictional-private-related-origin-check',Api__BaseUrl:'https://api.example.test/','Logging__LogLevel__Default':'Warning'},stdio:'ignore'});
 try{
  let response;for(let n=0;n<150;n++){assert.equal(child.exitCode,null);try{response=await get(port,host,'/health/live');if(response.status===200)break;}catch{}await sleep(100);}
  assert.equal(response?.status,200);response=await get(port,host,'/.well-known/webauthn');
  assert.equal(response.status,allowed?200:404);
  assert.equal(response.headers['cache-control'],'no-store');
  if(!allowed)assert.deepEqual(JSON.parse(response.body),{origins:[]});
  if(allowed){assert.match(response.headers['content-type'],/application\/json/);assert.deepEqual(JSON.parse(response.body),{origins:['https://auth-uat.swoovr.app']});assert.ok(!response.body.includes('untrusted.example.test'));}
 }finally{if(child.exitCode===null){const ended=once(child,'exit');child.kill('SIGTERM');await Promise.race([ended,sleep(5000)]);if(child.exitCode===null&&child.signalCode===null){child.kill('SIGKILL');await ended;}}}
});
