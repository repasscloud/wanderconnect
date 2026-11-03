const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),os=require('node:os');
const {spawnSync}=require('node:child_process');
const support=require('./owned-support.cjs'),{selectedSuites}=require('./run-owned.cjs');
function manifest(folder){const runId='a'.repeat(32);return{runId,database:'swoovr_registry_ui_'+runId,startedAt:Date.now(),api:'http://127.0.0.1:15880',web:'http://localhost:15881',admin:'http://localhost:15882',envPath:path.join(folder,'environment.env'),rootFixture:path.join(folder,'root.json'),journal:path.join(folder,'users.jsonl'),pgContainer:'swoovr-platform-postgres-1',pgPort:5433,memberClient:'swoovr-owned-web-'+runId,staffClient:'swoovr-owned-admin-'+runId,apiDll:path.resolve(__dirname,'../../src/Swoovr.Api/bin/Release/net10.0/Swoovr.Api.dll')};}
test('finite selectors require explicit nonduplicate reviewed suites',()=>{
 assert.deepEqual(selectedSuites('members,onboarding'),['members','onboarding']);
 assert.deepEqual(selectedSuites('phone,didit'),['phone','didit']);assert.deepEqual(selectedSuites('deletion'),['deletion']);
 for(const value of [undefined,'','members,members','../auth/root-admin.browser.cjs','meeting','members,','didit,deletion'])assert.throws(()=>selectedSuites(value));
});
test('paired overrides reject owner DB, nonloopback destinations, foreign paths and unowned clients',()=>{
 const folder=path.join(os.tmpdir(),'owned-guard-fixture'),file=path.join(folder,'manifest.json'),valid=manifest(folder);
 assert.equal(support.validateManifest(valid,file),valid);
 for(const change of [{database:'swoovr'},{web:'https://remote.example/'},{admin:valid.web},{rootFixture:'/tmp/owner-root.json'},{pgContainer:'owner-production-db'},{pgPort:5432},{memberClient:'swoovr-web'},{apiDll:'/tmp/other.dll'}])assert.throws(()=>support.validateManifest({...valid,...change},file));
 const env=Object.fromEntries(Object.entries(process.env).filter(([key])=>!key.startsWith('SWOOVR_TEST_')&&key!=='SWOOVR_OWNED_RUN_MANIFEST'));
 const child=spawnSync(process.execPath,['-e',"require('./tests/auth/local-tokens.cjs')"],{cwd:path.resolve(__dirname,'../..'),env:{...env,SWOOVR_TEST_WEB_BASE:valid.web},encoding:'utf8'});
 assert.notEqual(child.status,0);assert.match(child.stderr,/private owned-run manifest/);
});
test('dotenv parser treats values as data and preserves equals/quoted values',()=>{
 assert.deepEqual(support.parseEnv('A="a=b"\nB=literal\n# comment\nBAD LINE\n'),{A:'a=b',B:'literal'});
});
test('operator refusal preserves only explicit safe business diagnostics',()=>{
 assert.match(support.cliError('No single staff-realm account has that subject; sign in once first.'),/staff-realm/);
 assert.equal(support.cliError('Transport error Password=synthetic-sensitive'),'Owned API operator command failed (inspect private runner log).');
});
test('owned fixture emails preserve run proof within RFC local-part length including test renames',()=>{
 const value=manifest(os.tmpdir());
 for(const staff of[false,true])for(const label of['onboarding-adult','photo-support','a'.repeat(200)]){
  const email=support.fixtureEmail(value,label,staff);assert(email.includes(value.runId));assert(email.endsWith('@example.test'));
  assert(('changed.'+email).split('@')[0].length<=64);
 }
});
test('seeded Keycloak TOTP declares Base32 and matches an independent RFC6238 vector',async()=>{
 const secret='GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ',credential=support.otpCredential(secret),data=JSON.parse(credential.credentialData);
 assert.equal(data.secretEncoding,'BASE32');assert.equal(JSON.parse(credential.secretData).value,secret);
 const{totp}=await import('../../scripts/auth/totp.mjs');assert.equal(totp(secret,59,8),'94287082');
});
test('owned user creation tags and journals actual IDs, refuses foreign user writes',async()=>{
 const folder=await fs.mkdtemp(path.join(os.tmpdir(),'swoovr-owned-unit-')),value=manifest(folder),original=global.fetch;
 const id='11111111-1111-1111-1111-111111111111';let posted;const calls=[];
 try{
  await fs.writeFile(value.envPath,'KC_BOOTSTRAP_ADMIN_PASSWORD=synthetic-unit-only\n',{mode:0o600});
  global.fetch=async(url,options)=>{calls.push([url,options.method]);if(url.includes('/protocol/openid-connect/token'))return new Response(JSON.stringify({access_token:'synthetic-management',expires_in:60}));
   if(options.method==='POST'){posted=JSON.parse(options.body);return new Response(null,{status:201,headers:{Location:'http://localhost:8180/admin/realms/swoovr-members-dev/users/'+id}});}
   const foreign=url.includes('22222222-2222-2222-2222-222222222222');
   return new Response(JSON.stringify({id:foreign?'22222222-2222-2222-2222-222222222222':id,email:posted.email,username:posted.username,createdTimestamp:value.startedAt,attributes:{}}));};
  const api=await support.ownedRealm(value,'swoovr-members-dev');
  const email=support.fixtureEmail(value,'unit');
  await api('/users','POST',{email,username:email});
  assert.deepEqual(posted.attributes['swoovr.owned-run'],[value.runId]);
  assert.match(await fs.readFile(value.journal,'utf8'),new RegExp(id));
  const actual={id,email,username:email,createdTimestamp:value.startedAt,attributes:{}};
  assert.equal(await support.ownsUser(value,'swoovr-members-dev',actual),true,'stripped optional tags retain independent creation proof');
  for(const change of[{createdTimestamp:value.startedAt-1},{email:'owner@example.test'},{username:'owner'},{id:'22222222-2222-2222-2222-222222222222'}])assert.equal(await support.ownsUser(value,'swoovr-members-dev',{...actual,...change}),false);
  await assert.rejects(api('/users/22222222-2222-2222-2222-222222222222','DELETE'),/Unowned/);assert.equal(calls.some(([,method])=>method==='DELETE'),false);
  await assert.rejects(api('/clients/owner-client','PUT',{}),/Only owned user/);
 }finally{global.fetch=original;await fs.rm(folder,{recursive:true,force:true});}
});
