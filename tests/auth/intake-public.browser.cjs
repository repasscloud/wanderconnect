// Owned published Web host only: does not replace localhost services or create member credentials.
const assert=require('node:assert/strict'),{spawn}=require('node:child_process'),net=require('node:net'),fs=require('node:fs/promises');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'/Applications/ChatGPT.app/Contents/Resources/cua_node/lib/node_modules/playwright');
const {managedEdge}=require('./managed-edge.cjs');
(async()=>{
 const publish=process.env.SWOOVR_WEB_TEST_PUBLISH;if(!publish)throw Error('Set SWOOVR_WEB_TEST_PUBLISH to an isolated Web publish directory.');
 const probe=net.createServer();await new Promise((resolve,reject)=>{probe.once('error',reject);probe.listen(15881,'127.0.0.1',resolve)});await new Promise(resolve=>probe.close(resolve));
 const log=await fs.open('/tmp/swoovr-intake-public-host.log','w',0o600);
 const child=spawn('dotnet',[publish+'/Swoovr.Web.dll'],{cwd:publish,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:'http://localhost:15881',Keycloak__Authority:'http://localhost:18180/realms/synthetic-unused',Keycloak__ClientSecret:'synthetic-unused',Keycloak__RequireHttpsMetadata:'false'},stdio:['ignore',log.fd,log.fd]});await log.close();let edge;
 try{
  const base='http://localhost:15881';let ready=false;for(let i=0;i<150;i++){try{if((await fetch(base)).ok){ready=true;break}}catch{}await new Promise(r=>setTimeout(r,100))}assert(ready,'owned Web ready');
  edge=await managedEdge(chromium);const context=await edge.browser.newContext(),page=await context.newPage(),failures=[];
  const check=async(name,action)=>{try{await action()}catch(error){failures.push(name+': '+error.message)}};
  await check('guest account routing',async()=>{for(const route of ['/account','/account/']){const r=await fetch(base+route,{redirect:'manual'});assert.equal(r.status,302);assert.equal(r.headers.get('location'),'/account/sign-in');}});
  await check('retry remains reachable',async()=>{const r=await fetch(base+'/account?signin=retry',{redirect:'manual'});assert.equal(r.status,200);assert.match(await r.text(),/data-signin-retry/)});
  await check('BFF privacy',async()=>{const r=await fetch(base+'/bff/api/me',{headers:{'X-Swoovr-Bff':'1'},redirect:'manual'});assert.equal(r.status,401)});
  await page.goto(base);
  await check('guest navigation',async()=>{assert.equal(await page.locator('[data-tab]').count(),0);assert(await page.getByRole('link',{name:'Sign in',exact:true}).count()>0);assert(await page.getByRole('link',{name:'Create account',exact:true}).count()>0)});
  await check('landing accuracy',async()=>{assert.doesNotMatch(await page.locator('body').innerText(),/Member experiences and sign-in are not connected/);assert.match(await page.locator('body').innerText(),/private beta|in development/i)});
  for(const width of [375,768,1440]){await page.setViewportSize({width,height:900});await check('reflow '+width,async()=>assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)));if(width===375||width===1440)await page.screenshot({path:'/tmp/swoovr-intake-public-'+width+'.png',fullPage:true});}
  await context.close();assert.equal(failures.length,0,failures.join('\n'));
  console.log('PASS: actual Edge guest landing/navigation/reflow; HTTP account routing, retry and BFF privacy.');
 }finally{if(edge)await edge.close();if(child.exitCode===null){const done=new Promise(resolve=>child.once('exit',resolve));child.kill('SIGTERM');await done;}console.log('CLEANUP: owned Web and browser stopped.');}
})().catch(e=>{console.error(e.message);process.exitCode=1});
