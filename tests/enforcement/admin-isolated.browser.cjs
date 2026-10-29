// Actual SSR at its registered localhost callback; disposable API/database/media behind it.
// Preserves ordinary API/Web hosts and restores the positively identified main/worktree Admin checkout after testing.
const {execFile,spawn}=require('node:child_process'),{once}=require('node:events');
const fs=require('node:fs/promises'),path=require('node:path'),os=require('node:os'),net=require('node:net'),crypto=require('node:crypto');
const {sql,localEnv}=require('../auth/local-tokens.cjs');
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const run=(command,args,options={})=>new Promise((resolve,reject)=>execFile(command,args,options,(error,out,err)=>error?reject(new Error(command+' failed: '+err.slice(-1500))):resolve(out.trim())));
(async()=>{
 const staffAccess=process.env.SWOOVR_TEST_STAFF_ACCESS;
 if(staffAccess!==undefined&&staffAccess!=='1')throw new Error('Use only the explicit staff-access suite selector.');
 const suite=staffAccess==='1'?'tests/staff/access.browser.cjs':'tests/enforcement/admin.browser.cjs';
 const root=process.cwd(),database='swoovr_registry_ui_'+crypto.randomUUID().replaceAll('-','');
 const folder=await fs.mkdtemp(path.join(os.tmpdir(),'swoovr-registry-ui-'));await fs.chmod(folder,0o700);
 const backup=path.join(root,'.local/log-backups','registry-isolated-ui-'+new Date().toISOString().replaceAll(/[-:.]/g,''));await fs.mkdir(backup,{recursive:true,mode:0o700});
 await fs.copyFile('.local/swoovr-root-admin.json',path.join(backup,'swoovr-root-admin.json'));await fs.chmod(path.join(backup,'swoovr-root-admin.json'),0o600);
 const probe=net.createServer();await new Promise(resolve=>probe.listen(0,'127.0.0.1',resolve));const base='http://127.0.0.1:'+probe.address().port;await new Promise(resolve=>probe.close(resolve));
 const staffSecret=await localEnv('SWOOVR_STAFF_WEB_SECRET');
 const adminEnv={...process.env,DOTNET_ENVIRONMENT:'Development',ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:'http://localhost:5082',
  Keycloak__Authority:'http://localhost:8180/realms/swoovr-staff-dev',Keycloak__ClientSecret:staffSecret,Keycloak__RequireHttpsMetadata:'false',Api__BaseUrl:base+'/'};
 const env={...process.env,DOTNET_ENVIRONMENT:'Development',ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:base,
  ConnectionStrings__Swoovr:`Host=127.0.0.1;Port=5433;Database=${database};Username=swoovr;Password=${await localEnv('SWOOVR_DB_PASSWORD')}`,
  Founders__Secrets__1:await localEnv('SWOOVR_FOUNDER_SECRET_V1'),Staff__LocalPasswords:'true',MemberAdmin__ClientSecret:await localEnv('SWOOVR_MEMBERS_API_ADMIN_SECRET'),
  Beta__Enabled:'false',Phone__Required:'false',Accounts__ReauthSeconds:'45',Email__Smtp__Host:'127.0.0.1',Email__Smtp__Port:'1025',
  Media__Store:'local',Media__Local__Path:path.join(folder,'media'),Media__Screening__Worker:'false',Retention__Worker:'false'};
 const apiDir=path.join(root,'src/Swoovr.Api'),adminDir=path.join(root,'src/Swoovr.Admin');
 const apiDll=path.join(apiDir,'bin/Debug/net10.0/Swoovr.Api.dll'),adminDll=path.join(adminDir,'bin/Debug/net10.0/Swoovr.Admin.dll');
 let apiHost,adminHost,created=false,replaced=false,restoreRoot=root;
 const start=async(dll,cwd,settings,log)=>{const file=await fs.open(log,'w',0o600);const child=spawn('dotnet',Array.isArray(dll)?dll:[dll],{cwd,env:settings,stdio:['ignore',file.fd,file.fd],detached:true});await file.close();return child;};
 const healthy=async(url,child)=>{for(let n=0;n<150;n++){if(child.exitCode!==null||child.signalCode!==null)throw new Error('Owned test host exited before readiness.');try{if((await fetch(url,{signal:AbortSignal.timeout(1000)})).ok)return;}catch{}await sleep(100);}throw new Error('Owned host readiness timeout.');};
 const stop=async(child)=>{if(!child||child.exitCode!==null||child.signalCode!==null)return;const exited=once(child,'exit');process.kill(-child.pid,'SIGTERM');await Promise.race([exited,sleep(5000)]);if(child.exitCode===null&&child.signalCode===null){process.kill(-child.pid,'SIGKILL');await exited;}};
 try{
  await sql('CREATE DATABASE '+database);created=true;await run('dotnet',[apiDll,'migrate'],{cwd:apiDir,env});
  apiHost=await start(apiDll,apiDir,env,path.join(folder,'api.log'));await healthy(base+'/health/live',apiHost);
  // Only replace a positively identified main/worktree Admin; validate every listener before any signal.
  const mainRoot=path.dirname(await fs.realpath(path.join(root,'.local')));
  const allowed=new Set([adminDir,path.join(mainRoot,'src/Swoovr.Admin')]);
  const listeners=[...new Set((await run('lsof',['-tiTCP:5082','-sTCP:LISTEN'])).split('\n'))],owners=new Set();
  for(const id of listeners){
   const cwd=(await run('lsof',['-a','-p',id,'-d','cwd','-Fn'])).split('\n').find(line=>line.startsWith('n'))?.slice(1);
   if(!allowed.has(cwd))throw new Error('Admin listener is not owned by this worktree or its shared-local main; refusing replacement.');
   owners.add(path.dirname(path.dirname(cwd)));
  }
  if(owners.size!==1)throw new Error('Mixed Admin checkout listeners; refusing replacement.');
  restoreRoot=[...owners][0];
  replaced=true;
  for(const id of listeners)try{process.kill(Number(id),'SIGTERM');}catch(error){if(error.code!=='ESRCH')throw error;}
  await sleep(1000);
  adminHost=await start(adminDll,adminDir,adminEnv,path.join(folder,'admin.log'));await healthy('http://localhost:5082/signed-out',adminHost);
  console.log(await run('node',[suite],{cwd:root,env:{...env,SWOOVR_TEST_API_BASE:base,SWOOVR_TEST_APP_DATABASE:database},maxBuffer:1024*1024}));
 }finally{
  await stop(adminHost);await stop(apiHost);
  for(const name of ['api.log','admin.log'])try{await fs.copyFile(path.join(folder,name),path.join(backup,name));await fs.chmod(path.join(backup,name),0o600);}catch(error){if(error.code!=='ENOENT')throw error;}
  if(replaced){
   // Preserve the actual shared log before restoring its normal worktree Admin process.
   await fs.copyFile('.local/logs/admin.log',path.join(backup,'previous-shared-admin.log'));await fs.chmod(path.join(backup,'previous-shared-admin.log'),0o600);
   const restored=await start(['run','--project','src/Swoovr.Admin','--no-build','--no-launch-profile'],restoreRoot,{...adminEnv,Api__BaseUrl:'http://127.0.0.1:5080/'},path.join(root,'.local/logs/admin.log'));
   restored.unref();await healthy('http://localhost:5082/signed-out',restored);
  }
  if(created)await sql('DROP DATABASE '+database+' WITH (FORCE)');await fs.rm(folder,{recursive:true,force:true});
  console.log('CLEANUP: isolated registry UI database/API removed; original Admin checkout restored. Private logs: '+backup);
 }
})().catch(error=>{console.error(error);process.exitCode=1;});
