// Local macOS Edge tests, with an owned temporary profile and an explicit process lifecycle.
// Edge updater/crash helpers can inherit Playwright launch pipes and prevent browser.close from settling
// after the actual engine exits. A regular-file/ignored stdio launch plus documented CDP attachment avoids
// owning those updater streams. Never connect to a personal browser/profile or stop the shared updater.
const {spawn}=require('node:child_process'),{once}=require('node:events');
const fs=require('node:fs/promises'),os=require('node:os'),path=require('node:path');
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
async function managedEdge(chromium){
 const executable=process.env.SWOOVR_EDGE_EXECUTABLE||'/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge';
 if(!path.isAbsolute(executable))throw new Error('The test Edge executable must be an absolute path.');
 await fs.access(executable);
 const profile=await fs.mkdtemp(path.join(os.tmpdir(),'swoovr-edge-'));await fs.chmod(profile,0o700);
 const child=spawn(executable,['--headless=new','--no-first-run','--no-default-browser-check','--disable-background-networking','--disable-component-update',
  '--remote-debugging-address=127.0.0.1','--remote-debugging-port=0','--user-data-dir='+profile,'about:blank'],{stdio:'ignore'});
 let spawnError,browser,closing;child.once('error',error=>{spawnError=error;});
 const exited=()=>child.exitCode!==null||child.signalCode!==null;
 const stop=async()=>{
  if(child.pid&&!exited()){
   const done=once(child,'exit');child.kill('SIGTERM');await Promise.race([done,sleep(5000)]);
   if(!exited()){child.kill('SIGKILL');await done;}
  }
  await fs.rm(profile,{recursive:true,force:true});
 };
 try{
  let port;
  for(let attempt=0;attempt<300;attempt++){
   if(spawnError)throw spawnError;if(exited())throw new Error('Owned Edge exited before its local DevTools endpoint was ready.');
   try{port=Number((await fs.readFile(path.join(profile,'DevToolsActivePort'),'utf8')).split('\n')[0]);}catch(error){if(error.code!=='ENOENT')throw error;}
   if(Number.isInteger(port)&&port>0&&port<65536)break;port=undefined;await sleep(100);
  }
  if(!port)throw new Error('Owned Edge did not create its local DevTools endpoint.');
  browser=await chromium.connectOverCDP('http://127.0.0.1:'+port,{timeout:15000});
  return{browser,close:()=>closing??=(async()=>{try{await browser.close();}finally{await stop();}})()};
 }catch(error){await stop();throw error;}
}
module.exports={managedEdge};
