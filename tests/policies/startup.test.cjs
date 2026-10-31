// Actual compiled API startup: configuration cannot turn an unapproved/draft version into binding policies.
const {test}=require('node:test'),assert=require('node:assert/strict'),path=require('node:path'),fs=require('node:fs'),{spawnSync}=require('node:child_process');
const api=path.resolve('src/Swoovr.Api'),dll=path.join(api,'bin/Release/net10.0/Swoovr.Api.dll');
for(const selected of ['draft-2026-10-30-0.1','fictional-unapproved','2026-11-01'])test('startup refuses unapproved policy selection '+selected,()=>{
 assert.ok(fs.existsSync(dll),'Build Swoovr.Local.sln Release before startup checks.');
 const run=spawnSync('dotnet',[dll],{cwd:api,encoding:'utf8',timeout:15000,maxBuffer:1024*1024,env:{...process.env,DOTNET_ENVIRONMENT:'Development',ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:'http://127.0.0.1:0',ConnectionStrings__Swoovr:'',Policies__RequiredVersion:selected}});
 assert.ifError(run.error);assert.notEqual(run.status,0);assert.match(run.stderr,/No matching effective approved policy exists; drafts cannot be activated/);assert.doesNotMatch(run.stdout,/Now listening on/);
});
