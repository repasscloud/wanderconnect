#!/usr/bin/env node
// Guided fresh registration/real-provider checkpoints, then automatic authenticated BFF core journey.
const {journeyCore,ensure}=require('./journey-core.cjs');
const readline=require('node:readline/promises');
function origin(value){const u=new URL(value);ensure(u.origin===value&&!u.username&&!u.password&&(u.protocol==='https:'||(u.protocol==='http:'&&['localhost','127.0.0.1'].includes(u.hostname))),'explicit HTTPS or loopback base origin');return u.origin;}
async function main(){
 const args=process.argv.slice(2);ensure(args.length===2&&args[0]==='--base-url','usage: --base-url https://uat.swoovr.app');const base=origin(args[1]);
 ensure(process.stdin.isTTY,'interactive operator terminal required; no credential files or hidden automation');
 const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
 const edge=await require('../../tests/auth/managed-edge.cjs').managedEdge(chromium,{headless:false});const browser=edge.browser;const terminal=readline.createInterface({input:process.stdin,output:process.stdout});
 const abort=()=>{console.error('UAT smoke interrupted/timed out; no completion claim.');setTimeout(()=>process.exit(2),10000).unref();edge.close().finally(()=>process.exit(2));};
 process.once('SIGINT',abort);process.once('SIGTERM',abort);const timer=setTimeout(abort,30*60*1000);
 const clients=[];
 try{
  console.log('Two fresh approved beta participants are required. Real verification stays in the browser/provider. No screenshots, traces, HAR, storage state, credentials, IDs or response bodies are saved. This creates a connection, fictional message and a clearly marked test report/block.');
  ensure(await terminal.question('Type RUN to perform these UAT test mutations: ')==='RUN','operator mutation acknowledgement');
  for(const label of ['A','B']){
   const context=await browser.newContext();const page=await context.newPage();let registered=false;
   page.on('request',r=>{try{if(r.method()==='POST'&&new URL(r.url()).pathname.endsWith('/login-actions/registration'))registered=true;}catch{}});
   const call=async(path,{method='GET',body}={})=>{ensure(path.startsWith('/api/'),'member API route');const response=await context.request.fetch(base+'/bff'+path,{method,headers:{'X-Swoovr-Bff':'1','Swoovr-Version':'2026-10-28'},...(body===undefined?{}:{data:body}),timeout:60000,maxRedirects:0});return {status:response.status(),body:response.status()===204?null:await response.json().catch(()=>null)};};
   await page.goto(base+'/account/sign-in');
   console.log('Participant '+label+': choose Register; complete real email verification and required phone/identity/policy steps; build a visible Friends profile with your truthful adult details in the same home city as the other participant. Do not use an existing login or bypass provider checks.');
   ensure(await terminal.question('When participant '+label+' is ready in the browser, type READY: ')==='READY','participant checkpoint');
   ensure(registered,'registration submission observed');clients.push(call);console.log('PASS participant '+label+' registration submission observed.');
  }
  await journeyCore(clients[0],clients[1],step=>console.log('PASS '+step));
  console.log('PASS guided registration/email/profile/discovery/request/accept/message/report/block journey. Registration submission and verified member sessions were checked; actual mailbox/provider/human checks require the operator checkpoint evidence; device push, staff acceptance and restore remain separate. Retained test accounts/report follow ordinary retention/deletion; do not delete audit evidence directly.');
 }finally{clearTimeout(timer);process.removeListener('SIGINT',abort);process.removeListener('SIGTERM',abort);terminal.close();await edge.close();}
}
if(require.main===module)main().catch(()=>{console.error('UAT smoke failed or incomplete; no success claim. Inspect the private browser/operator steps; no payload is logged.');process.exitCode=1;});
module.exports={origin};
