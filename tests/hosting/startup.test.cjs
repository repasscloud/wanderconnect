// Actual compiled host refusal; no configured provider request, credentials or cloud environment.
const {test}=require('node:test'),assert=require('node:assert/strict'),path=require('node:path'),{spawnSync}=require('node:child_process');
for(const host of ['Api','Web','Admin'])for(const [key,value,code] of [
 ['Staff__LocalPasswords','true','development-only:Staff:LocalPasswords'],['Prototype__Enabled','true','development-only:Prototype:Enabled'],
 ['Evidence__HoldCheckUnavailable','true','development-only:Evidence:HoldCheckUnavailable'],['DetailedErrors','true','development-only:DetailedErrors'],
 ['Media__Screening__RequireKnownContentCheck','false','known-content-check-required'],['Sms__Provider','simulated','development-only:Sms:Provider'],
 ['Push__Provider','simulated','development-only:Push:Provider'],['Keycloak__RequireHttpsMetadata','false','https-metadata-required']
])test(host+' refuses hosted '+key,()=>{
 const cwd=path.resolve('src/Swoovr.'+host),dll=path.join(cwd,'bin/Release/net10.0/Swoovr.'+host+'.dll');
 const run=spawnSync('dotnet',[dll],{cwd,encoding:'utf8',timeout:15000,maxBuffer:1024*1024,env:{...process.env,DOTNET_ENVIRONMENT:'Uat',ASPNETCORE_ENVIRONMENT:'Uat',ASPNETCORE_URLS:'http://127.0.0.1:0',[key]:value}});
 assert.ifError(run.error);assert.notEqual(run.status,0);assert.match(run.stderr,/Hosted configuration rejected:/);assert.ok(run.stderr.includes(code));assert.doesNotMatch(run.stdout,/Now listening on/);
});

for(const host of ['Api','Web','Admin'])test(host+' refuses unconfigured hosted defaults',()=>{
 const cwd=path.resolve('src/Swoovr.'+host),dll=path.join(cwd,'bin/Release/net10.0/Swoovr.'+host+'.dll');
 const run=spawnSync('dotnet',[dll],{cwd,encoding:'utf8',timeout:15000,maxBuffer:1024*1024,env:{...process.env,DOTNET_ENVIRONMENT:'Uat',ASPNETCORE_ENVIRONMENT:'Uat',ASPNETCORE_URLS:'http://127.0.0.1:0'}});
 assert.ifError(run.error);assert.notEqual(run.status,0);assert.match(run.stderr,/Hosted configuration rejected:/);assert.match(run.stderr,/unsafe-url:Hosting:PublicOrigin/);assert.doesNotMatch(run.stdout,/Now listening on/);
});
