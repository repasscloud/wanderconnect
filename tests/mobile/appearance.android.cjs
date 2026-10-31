// Agent/operator-driven emulator UI with independent PostgreSQL assertions. No hosted/device-push claim.
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),os=require('node:os');
if(!process.env.SWOOVR_TEST_APP_DATABASE||process.env.SWOOVR_NATIVE_UI!=='1'||process.env.SWOOVR_MEETING_API_PORT!=='5400')throw Error('Explicit owned native fixture/port required.');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright'),{managedEdge}=require('../auth/managed-edge.cjs');
const h=require('../auth/local-tokens.cjs'),sleep=ms=>new Promise(r=>setTimeout(r,ms));
let interrupted=false;for(const signal of ['SIGTERM','SIGINT'])process.once(signal,()=>{interrupted=true;});
(async()=>{const edge=await managedEdge(chromium);let folder;try{
 const member=await h.createMember('native-appearance');await h.cli('beta','add',member.email);const token=await h.memberToken(edge.browser,member);assert.equal((await h.call(token,'/api/me')).status,200);
 const sub=h.claims(token).sub;assert.match(sub,/^[0-9a-f-]{36}$/i);const account=await h.sql("SELECT id FROM accounts WHERE subject='"+sub+"'");assert.match(account,/^[0-9a-f-]{36}$/i);
 folder=await fs.mkdtemp(path.join(os.tmpdir(),'swoovr-native-appearance-'));await fs.chmod(folder,0o700);const fixture=path.join(folder,'fixture.json');await fs.writeFile(fixture,JSON.stringify({email:member.email,password:member.password}),{mode:0o600});
 console.log('READY_NATIVE_UI '+fixture);console.log('Use the owned Development Debug APK: choose guest Coral/System, sign in, choose account Pride/Dark, then sign out and confirm guest Coral/System.');
 const until=Date.now()+8*60_000;let saved=false;
 while(Date.now()<until&&!interrupted){if(await h.sql("SELECT scheme||'/'||mode FROM member_appearances WHERE account_id='"+account+"'")==='pride/dark'){saved=true;break;}await sleep(1000);}
 assert.ok(saved,'native UI did not persist Pride/Dark within its bounded window');console.log('SERVER_ASSERTION: authenticated native appearance persisted. Write fixture.json.done only after observing logout guest restoration.');
 let done=false;while(Date.now()<until&&!interrupted){try{await fs.access(fixture+'.done');done=true;break;}catch{}await sleep(1000);}
 assert.ok(done,'guest-restoration UI observation not acknowledged');assert.equal(await h.sql("SELECT scheme||'/'||mode FROM member_appearances WHERE account_id='"+account+"'"),'pride/dark');
 console.log('PASS native account appearance: 2 independent PostgreSQL assertions plus separately recorded agent-driven local Android OAuth/UI observations; no hosted/iOS/provider-delivery evidence.');
}finally{if(folder)await fs.rm(folder,{recursive:true,force:true});await edge.close();}})().catch(e=>{console.error(e.message);process.exitCode=1});
