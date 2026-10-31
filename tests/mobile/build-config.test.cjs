// Actual MSBuild evaluation/validation. Requires installed MAUI workloads; does not build/distribute an app.
const {test}=require('node:test'),assert=require('node:assert/strict'),{spawnSync}=require('node:child_process'),os=require('node:os'),path=require('node:path');
const project='src/Swoovr.Mobile/Swoovr.Mobile.csproj';
function evaluate(configuration,environment,extra={},framework='net10.0-android'){
 const properties={Configuration:configuration,TargetFramework:framework,...(environment?{SwoovrEnvironment:environment}:{}),...extra};
 const run=spawnSync('dotnet',['msbuild',project,'-nologo','-t:ValidateSwoovrBuild;ValidatePushBuild','-getProperty:SwoovrEnvironment,DefineConstants,ApplicationTitle','-getItem:PartialAppManifest',...Object.entries(properties).map(([k,v])=>'-p:'+k+'='+v)],{encoding:'utf8',timeout:30000,maxBuffer:1024*1024,env:{...process.env,JAVA_HOME:path.join(os.homedir(),'Library/Developer/Java/jdk'),ANDROID_HOME:path.join(os.homedir(),'Library/Android/sdk')}});
 assert.ifError(run.error);return run;
}
for(const [configuration,environment,expected] of [['Debug',null,'Development'],['Release',null,'Uat'],['Debug','Uat','Uat']])test('native target '+configuration+'/'+(environment??'default'),()=>{
 const run=evaluate(configuration,environment);assert.equal(run.status,0,run.stderr+run.stdout);const value=JSON.parse(run.stdout.slice(run.stdout.indexOf('{')));assert.equal(value.Properties.SwoovrEnvironment,expected);assert.equal(value.Properties.DefineConstants.includes('SWOOVR_UAT'),expected==='Uat');if(expected==='Uat')assert.equal(value.Properties.ApplicationTitle,'Swoovr UAT');
});
for(const [configuration,environment,extra,message] of [
 ['Release','Development',{},'Development endpoints require a Debug build'],['Release','Uat',{SwoovrOwnedApiTest:'true'},'Owned API testing requires a Development Debug build'],
 ['Debug','Uat',{SwoovrOwnedApiTest:'true'},'Owned API testing requires a Development Debug build'],['Debug','Production',{},'SwoovrEnvironment must be Development or Uat'],
 ['Debug','Uat',{EnableFcmPush:'true'},'external SwoovrGoogleServicesJson'],['Release','Uat',{EnableApnsPush:'true',ApnsEnvironment:'development'},'UAT Release APNs requires production entitlements']
])test('native rejects unsafe '+configuration+'/'+environment+'/'+Object.keys(extra).join(','),()=>{const run=evaluate(configuration,environment,extra);assert.notEqual(run.status,0);assert.ok((run.stdout+run.stderr).includes(message),run.stdout+run.stderr);});
test('owned API is explicit and Development-only',()=>{const run=evaluate('Debug','Development',{SwoovrOwnedApiTest:'true'});assert.equal(run.status,0,run.stdout+run.stderr);assert.ok(JSON.parse(run.stdout.slice(run.stdout.indexOf('{'))).Properties.DefineConstants.includes('SWOOVR_OWNED_API'));});
for(const environment of ['Development','Uat'])test('iOS local transport manifest '+environment,()=>{const run=evaluate('Debug',environment,{},'net10.0-ios');assert.equal(run.status,0,run.stdout+run.stderr);const value=JSON.parse(run.stdout.slice(run.stdout.indexOf('{')));assert.equal((value.Items.PartialAppManifest??[]).some(x=>x.Identity.includes('Debug.plist')),environment==='Development');});
