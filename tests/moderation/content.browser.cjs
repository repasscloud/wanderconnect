// #114: real owned API/PostgreSQL/Admin SSR/Web and two actual API SignalR clients.
// Fictional adult identities/text only. No native OS UI or real-world ownership/delivery claim.
// Execute ONLY through tests/meeting/isolated.browser.cjs with this exact finite regression selector.
const crypto=require('node:crypto'),fs=require('node:fs/promises'),path=require('node:path');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
const {managedEdge}=require('../auth/managed-edge.cjs');
const {api,web,admin,sleep,createMember,createStaff,memberToken,staffToken,rootAdminToken,cli,sql,call,claims,matchReady}=require('../auth/local-tokens.cjs');
if(!process.env.SWOOVR_TEST_API_BASE||!process.env.SWOOVR_TEST_APP_DATABASE)throw new Error('Use the owned meeting regression runner for content acceptance.');
const RS='\u001e',hubs=[];let phase='initialization',passed=0;
function check(value,label){if(!value){const error=new Error(label);error.safeLabel=true;throw error;}passed++;}
async function until(predicate,timeout=8000){for(let elapsed=0;elapsed<timeout;elapsed+=100){if(await predicate())return true;await sleep(100);}return false;}
async function listen(token){
 const base=api+'/hubs/messages',headers={Authorization:'Bearer '+token};
 const negotiated=await fetch(base+'/negotiate?negotiateVersion=1',{method:'POST',headers});
 check(negotiated.ok,'API hub negotiation accepted');
 const negotiation=await negotiated.json();
 check(negotiation.availableTransports.some(t=>t.transport==='LongPolling'),'owned API supports header-authenticated live SignalR long polling');
 // LongPolling keeps the bearer credential in Authorization headers, never the URL. The negotiated transport id
 // selects the authenticated connection; it is not a Swoovr login token/session. No transport URL is printed.
 const url=base+'?id='+encodeURIComponent(negotiation.connectionToken),received=[],abort=new AbortController();
 let handshake=false,failed=false,closed=false;
 const first=await fetch(url,{headers,signal:abort.signal});check(first.ok,'API hub transport opened');await first.text();
 const poll=(async()=>{
  try{while(!closed){const response=await fetch(url,{headers,signal:abort.signal});if(response.status===204)break;
   if(!response.ok){failed=true;break;}const text=await response.text();
   for(const frame of text.split(RS).filter(Boolean)){const message=JSON.parse(frame);
    if(!('type'in message)){handshake=!message.error;continue;}
    if(message.type===1)received.push({target:message.target,payload:message.arguments[0]});
   }
  }}catch{if(!closed)failed=true;}
 })();
 const send=data=>fetch(url,{method:'POST',headers:{...headers,'Content-Type':'text/plain;charset=UTF-8'},body:JSON.stringify(data)+RS,signal:abort.signal});
 check((await send({protocol:'json',version:1})).ok,'API hub protocol handshake submitted');
 check(await until(()=>handshake||failed)&&handshake,'API hub handshake accepted');
 const ping=setInterval(()=>{if(!closed)send({type:6}).catch(()=>{failed=true;});},10000);
 const hub={received,close:async()=>{closed=true;clearInterval(ping);abort.abort();await fetch(url,{method:'DELETE',headers}).catch(()=>{});await poll;}};
 hubs.push(hub);return hub;
}
async function login(page,base,user,destination){
 await page.goto(base+(base===web?'/account/sign-in?intro=done':'/sign-in'));
 await page.fill('#username',user.email);await page.fill('#password',user.password);
 await Promise.all([page.waitForURL(base+(destination|| (base===web?'/account':'/'))),page.click('#kc-login')]);
}
(async()=>{
 const edge=await managedEdge(chromium),browser=edge.browser;
 const contexts=[];const folder=path.resolve(process.env.SWOOVR_SCREENSHOTS||'.local/suite-shots','2026-10-30-content-removal');
 try{
  await fs.mkdir(folder,{recursive:true});phase='fictional-member-fixtures';
  const root=await rootAdminToken(browser),members={},tokens={},ids={},pids={};
  for(const name of ['reporter','subject','other'])members[name]=await createMember('content-'+name);
  await cli('beta','add',...Object.values(members).map(x=>x.email));
  for(const name of Object.keys(members)){
   tokens[name]=await memberToken(browser,members[name]);
   const profile=await call(tokens[name],'/api/me/profile',{method:'PUT',body:{displayName:'Fictional content '+name,homeCityId:2078025,about:'FICTIONAL PUBLIC CORE '+name}});
   check(profile.status===200,'fictional profile saved');pids[name]=profile.body.publicId;
   ids[name]=await sql(`SELECT id FROM accounts WHERE subject='${claims(tokens[name]).sub}'`);
   check((await call(tokens[name],'/api/me/modes/friends',{method:'PUT',body:{enabled:true,visible:true,about:'FICTIONAL FRIENDS TEXT '+name,shareCoreAbout:true}})).status===200,'fictional friends mode saved');
   check((await matchReady(tokens[name])).every(s=>s===200),'fictional adult matching prerequisites saved');
  }
  const staff={},st={},sid={};
  for(const [name,role]of [['adm','admin'],['sup','support'],['leg','legal'],['subjectConflict','admin'],['reporterConflict','admin']]){
   staff[name]=await createStaff('content-'+name);
   check((await call(root,'/api/admin/invitations',{method:'POST',body:{email:staff[name].email,role}})).status===201,'fictional staff invitation recorded');
   st[name]=await staffToken(browser,staff[name]);await call(st[name],'/api/session');
   sid[name]=await sql(`SELECT id FROM accounts WHERE subject='${claims(st[name]).sub}' AND realm='Staff'`);
  }
  const request=await call(tokens.reporter,'/api/requests',{method:'POST',body:{to:pids.subject,mode:'friends'}});
  check(request.status===201,'connection request created');
  check((await call(tokens.subject,`/api/requests/${request.body.id}/accept`,{method:'POST'})).status===200,'connection accepted');
  const opened=await call(tokens.reporter,'/api/conversations',{method:'POST',body:{to:pids.subject}});check(opened.status===200||opened.status===201,'conversation opened');
  const conversation=opened.body.id;
  const reportProfile=await call(tokens.reporter,'/api/reports',{method:'POST',body:{subjectType:'profile',subjectId:pids.subject,category:'harassment',text:'Fictional review fixture only',block:false}});
  check(reportProfile.status===201,'profile report submitted');
  const caseId=await sql(`SELECT id FROM moderation_cases WHERE subject_account_id='${ids.subject}' AND status IN ('Open','UnderReview')`);
  const foreign=await call(tokens.reporter,'/api/reports',{method:'POST',body:{subjectType:'profile',subjectId:pids.other,category:'harassment',text:'Separate fictional case fixture',block:false}});
  check(foreign.status===201,'separate profile case submitted');
  const foreignCase=await sql(`SELECT id FROM moderation_cases WHERE subject_account_id='${ids.other}' AND status IN ('Open','UnderReview')`);
  const sourcesPath=`/api/admin/cases/${caseId}/content-sources`,removePath=`/api/admin/cases/${caseId}/content-removals`;
  const preview=await call(st.adm,sourcesPath);check(preview.status===200&&Array.isArray(preview.body),'admin case source preview available');
  let core=preview.body.find(s=>s.reportId===reportProfile.body.id&&s.field==='about');
  check(core&&core.text==='FICTIONAL PUBLIC CORE subject'&&/^[0-9a-f]{64}$/.test(core.sha256),'preview supplies exact current text and body-only digest');
  const command=s=>({commandId:crypto.randomUUID(),reportId:s.reportId,field:s.field,modeId:s.modeId,messageId:s.messageId,expectedSourceSha256:s.sha256,reason:'Fictional individual-content review decision'});
  phase='authority-negatives';
  for(const token of [null,tokens.subject,st.leg]){
   const expected=token?403:401;
   check((await call(token,sourcesPath)).status===expected,'anonymous/member/legal source access refused');
   check((await call(token,removePath,{method:'POST',body:command(core)})).status===expected,'anonymous/member/legal mutation refused');
  }
  check((await call(st.sup,sourcesPath)).status===200,'support can review ordinary non-affiliated case');
  const badReport=command(core);badReport.reportId=foreign.body.id;
  check((await call(st.adm,removePath,{method:'POST',body:badReport})).status===404,'foreign report cannot authorize source removal');
  for(const [name,person]of [['subjectConflict',ids.subject],['reporterConflict',ids.reporter]]){
   check((await call(root,`/api/admin/staff-links/${sid[name]}`,{method:'PUT',body:{memberAccountId:person}})).status===204,'explicit fictional staff conflict recorded');
   check((await call(st[name],sourcesPath)).status===403,'subject or reporter conflict refuses exact source preview');
   check((await call(st[name],removePath,{method:'POST',body:command(core)})).status===403,'subject or reporter conflict refuses removal');
  }
  phase='stale-source';
  const changedCore='FICTIONAL REVISED CORE TO REVIEW';
  check((await call(tokens.subject,'/api/me/profile',{method:'PUT',body:{displayName:'Fictional content subject',homeCityId:2078025,about:changedCore}})).status===200,'member can revise own source before review');
  const stale=await call(st.adm,removePath,{method:'POST',body:command(core)});
  check(stale.status===409&&stale.body.code==='content-source-changed','stale reviewed digest refuses removal');
  check(await sql(`SELECT count(*) FROM content_removal WHERE case_id='${caseId}'`)==='0','stale request adds no removal');
  core=(await call(st.adm,sourcesPath)).body.find(s=>s.reportId===reportProfile.body.id&&s.field==='about');
  check(core.text===changedCore,'fresh source preview reflects actual edit');

  phase='admin-ssr-explicit-review';
  const adminContext=await browser.newContext({viewport:{width:390,height:844}});contexts.push(adminContext);const page=await adminContext.newPage();
  await login(page,admin,staff.adm);
  // Only opaque IDs and fixed field names are in navigation; source text/hash/reason stay in SSR response/form body.
  const review=admin+`/cases/${caseId}/content?report=${reportProfile.body.id}&field=about`;
  await page.goto(review);await page.locator('[data-content-source]').waitFor();
  check(await page.locator('[data-content-source]').textContent()===changedCore,'real Admin SSR shows the exact reviewed source');
  check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Admin source review fits390px');
  await page.screenshot({path:path.join(folder,'admin-source-review-390.png'),fullPage:true});
  await page.fill('[name="RemovalForm.Reason"]','Fictional SSR single-field removal reason');
  await Promise.all([page.waitForNavigation({waitUntil:'domcontentloaded'}),page.locator('[data-content-removal-form]').evaluate(form=>{form.noValidate=true;form.requestSubmit();})]);
  await page.locator('[data-content-result="confirmation-required"]').waitFor();
  check(await sql(`SELECT count(*) FROM content_removal WHERE case_id='${caseId}'`)==='0','real SSR POST refuses unchecked confirmation');
  await page.fill('[name="RemovalForm.Reason"]','Fictional SSR single-field removal reason');
  await page.selectOption('[name="RemovalForm.PolicyBasis"]','harassment');
  await page.check('[name="RemovalForm.Confirmed"]');
  await Promise.all([page.waitForNavigation({waitUntil:'domcontentloaded'}),page.click('[data-remove-content]')]);check(await page.locator('[data-content-result]').getAttribute('data-content-result')==='ok','confirmed SSR form records the reviewed content action');
  await page.screenshot({path:path.join(folder,'admin-removal-recorded-390.png'),fullPage:true});
  const coreId=await sql(`SELECT id FROM content_removal WHERE case_id='${caseId}' AND field='about'`);
  check(/^[0-9a-f-]{36}$/.test(coreId),'SSR mutation durably records an individual removal');
  check(await sql(`SELECT reviewed_policy_basis||'/'||detection_mechanism FROM moderation_transparency_facts WHERE source_id='${coreId}'`)==='harassment/member-report','SSR action records explicit basis and actual same-case report source');
  let own=(await call(tokens.subject,'/api/me/profile')).body;
  check(own.about===null&&own.displayName==='Fictional content subject'&&own.modes.find(m=>m.mode==='friends'||m.modeId==='friends'||m.id==='friends')?.about==='FICTIONAL FRIENDS TEXT subject','core removal leaves display name and mode text intact');
  phase='mode-field-only';
  const keepCore='FICTIONAL CORE FALLBACK MUST NOT REAPPEAR';
  await call(tokens.subject,'/api/me/profile',{method:'PUT',body:{displayName:'Fictional content subject',homeCityId:2078025,about:keepCore}});
  const modeSource=(await call(st.sup,sourcesPath)).body.find(s=>s.reportId===reportProfile.body.id&&s.field==='mode-about'&&s.modeId==='friends');
  check(modeSource&&modeSource.text==='FICTIONAL FRIENDS TEXT subject','support sees exact reported mode source');
  const modeRemoval=await call(st.sup,removePath,{method:'POST',body:command(modeSource)});check(modeRemoval.status===200,'support removes one ordinary case mode field');
  const supportEvidence=await call(st.sup,removePath+'/'+modeRemoval.body.id);check(supportEvidence.status===200&&supportEvidence.body.text===modeSource.text,'authorized support can reopen same-case ordinary retained evidence');
  own=(await call(tokens.subject,'/api/me/profile')).body;
  const mode=own.modes.find(m=>m.mode==='friends'||m.modeId==='friends'||m.id==='friends');
  check(own.about===keepCore&&mode?.about===null&&mode.shareCoreAbout===false,'mode removal preserves core and disables fallback');
  const shown=await call(tokens.reporter,`/api/profiles/${pids.subject}?mode=friends`);
  check(shown.status===200&&!JSON.stringify(shown.body).includes(keepCore),'member-facing mode does not reveal core fallback');

  phase='reported-message-live-clients';
  const original='FICTIONAL REPORTED MESSAGE FOR CONTENT REVIEW';
  const sent=await call(tokens.subject,`/api/conversations/${conversation}/messages`,{method:'POST',body:{text:original}});
  check(sent.status===201||sent.status===200,'fictional message sent');const messageId=sent.body.id;
  const kept=await call(tokens.subject,`/api/conversations/${conversation}/messages`,{method:'POST',body:{text:'FICTIONAL SECOND MESSAGE KEPT'}});
  const reported=await call(tokens.reporter,'/api/reports',{method:'POST',body:{subjectType:'message',subjectId:messageId,category:'threats',text:'Fictional urgent-category routing fixture only',block:false}});
  check(reported.status===201,'single message reported through actual member route');
  check((await call(st.sup,sourcesPath)).status===403,'support cannot review promoted admin-queue sources');
  const msgSource=(await call(st.adm,sourcesPath)).body.find(s=>s.reportId===reported.body.id&&s.field==='message'&&s.messageId===messageId);
  check(msgSource?.text===original,'message preview is tied to exact report snapshot');
  const wrongMessage=command(msgSource);wrongMessage.messageId=kept.body.id;
  check((await call(st.adm,removePath,{method:'POST',body:wrongMessage})).status===409,'unchosen message cannot be removed through another report');
  const a=await listen(tokens.reporter),b=await listen(tokens.subject);
  const webContext=await browser.newContext({viewport:{width:390,height:844}});contexts.push(webContext);const thread=await webContext.newPage();
  // Playwright WebSocketRoute APIs added1.48 (supported by the pinned1.57 module):
  // https://playwright.dev/docs/api/class-websocketroute#web-socket-route-connect-to-server
  // Only actual transport connectivity is interrupted. No auth or SignalR application frames are fabricated.
  const browserHub=/\/bff\/hubs\/messages(?:[/?]|$)/;
  let blockBrowserHub=false,blockedNegotiations=0,observedFailedNegotiations=0;
  const browserSockets=[];
  await webContext.routeWebSocket(browserHub,async socket=>{
   if(blockBrowserHub){await socket.close({code:1013,reason:'Owned temporary transport interruption'});return;}
   const server=socket.connectToServer(); // All real frames pass through unchanged in both directions.
   browserSockets.push({socket,server});
  });
  await webContext.route(browserHub,async route=>{
   if(blockBrowserHub){if(new URL(route.request().url()).pathname.endsWith('/negotiate'))blockedNegotiations++;
    await route.abort('internetdisconnected');return;}
   await route.continue();
  });
  thread.on('requestfailed',request=>{
   if(new URL(request.url()).pathname==='/bff/hubs/messages/negotiate')observedFailedNegotiations++;
  });

  await login(thread,web,members.reporter);await thread.goto(web+'/chats/'+conversation);
  await thread.locator(`[data-message="${messageId}"]`).waitFor();
  check(await thread.evaluate(async route=>(await fetch('/bff'+route,{headers:{'X-Swoovr-Bff':'1'}})).status,sourcesPath)===404,'member BFF never forwards admin content-source route');
  check(await until(async()=>await thread.locator('[data-conversation="ready"][data-live="on"]').count()>0),'real Web thread hub is live before removal');
  const body=command(msgSource),result=await call(st.adm,removePath,{method:'POST',body});
  check(result.status===200&&!result.body.alreadyApplied,'message removal applied once');
  const typed=event=>event.target==='message'&&event.payload.id===messageId&&event.payload.kind==='removed-by-moderation';
  check(await until(()=>a.received.some(typed)&&b.received.some(typed)),'both actual API hubs receive the existing typed message event');
  for(const hub of [a,b]){const event=hub.received.find(typed).payload;check(event.text==='Removed by Swoovr.'&&event.deleted===false&&event.canDelete===false,'typed tombstone differs from member deletion');}
  await thread.locator(`[data-message="${messageId}"][data-message-kind="removed-by-moderation"]`).waitFor();
  check(!(await thread.locator(`[data-message="${messageId}"]`).textContent()).includes(original),'live Web replaces original without page reload');
  await thread.screenshot({path:path.join(folder,'web-live-removed-message-390.png'),fullPage:true});
  for(const who of ['reporter','subject']){
   const messages=(await call(tokens[who],`/api/conversations/${conversation}/messages`)).body.items;
   const removed=messages.find(m=>m.id===messageId);
   check(removed?.kind==='removed-by-moderation'&&removed.text==='Removed by Swoovr.'&&!removed.deleted&&!removed.canDelete,'both member GET projections retain neutral typed state');
   check(messages.find(m=>m.id===kept.body.id)?.text==='FICTIONAL SECOND MESSAGE KEPT','unselected message remains intact');
  }
  const events=a.received.filter(typed).length+b.received.filter(typed).length;
  const notices=await sql(`SELECT count(*) FROM member_notices WHERE account_id='${ids.subject}' AND kind='ContentRemoved'`);
  const replay=await call(st.adm,removePath,{method:'POST',body});check(replay.status===200&&replay.body.alreadyApplied,'same reviewed command replays idempotently');await sleep(350);
  check(a.received.filter(typed).length+b.received.filter(typed).length===events,'replay emits no additional typed message event');
  check(await sql(`SELECT count(*) FROM member_notices WHERE account_id='${ids.subject}' AND kind='ContentRemoved'`)===notices,'replay creates no extra member notice');

  phase='actual-web-automatic-reconnect';
  const offlineOriginal='FICTIONAL MESSAGE REMOVED WHILE WEB TRANSPORT IS INTERRUPTED';
  const offlineSent=await call(tokens.subject,`/api/conversations/${conversation}/messages`,{method:'POST',body:{text:offlineOriginal}});
  check(offlineSent.status===201||offlineSent.status===200,'offline-race fictional message created through actual API');
  const offlineId=offlineSent.body.id;
  await thread.locator(`[data-message="${offlineId}"]`).waitFor();
  check((await thread.locator(`[data-message="${offlineId}"]`).textContent()).includes(offlineOriginal),'actual open Web thread cached source before interruption');
  const offlineReport=await call(tokens.reporter,'/api/reports',{method:'POST',body:{subjectType:'message',subjectId:offlineId,category:'threats',text:'Fictional disconnected-client review fixture only',block:false}});
  check(offlineReport.status===201,'offline-race exact message report submitted');
  const offlineSource=(await call(st.adm,sourcesPath)).body.find(source=>source.reportId===offlineReport.body.id&&source.messageId===offlineId&&source.field==='message');
  check(offlineSource?.text===offlineOriginal,'offline-race source is exact reported current message');
  check(browserSockets.length>0,'real browser hub socket was connected through unchanged forwarding');
  const socketCountBefore=browserSockets.length,documentMarker=crypto.randomUUID();
  await thread.evaluate(marker=>{window.__contentReconnectDocument=marker;},documentMarker);
  let threadNavigations=0,currentHistoryRefreshes=0;
  thread.on('framenavigated',frame=>{if(frame===thread.mainFrame())threadNavigations++;});
  thread.on('response',response=>{
   const request=response.request();
   if(request.method()==='GET'&&new URL(request.url()).pathname===`/bff/api/conversations/${conversation}/messages`&&response.ok())currentHistoryRefreshes++;
  });
  blockBrowserHub=true;
  await browserSockets.at(-1).socket.close({code:1012,reason:'Owned actual connection interruption'});
  check(await until(()=>blockedNegotiations>0&&observedFailedNegotiations>0,12000),'actual WASM automatic reconnect negotiated and failed while transport gate was closed');
  const offlineCommand=command(offlineSource),offlineRemoved=await call(st.adm,removePath,{method:'POST',body:offlineCommand});
  check(offlineRemoved.status===200&&!offlineRemoved.body.alreadyApplied,'staff removes exact message through independent Node API during browser transport interruption');
  // The application receives no replacement frame while disconnected; this is the cached-text race under test.
  check((await thread.locator(`[data-message="${offlineId}"]`).textContent()).includes(offlineOriginal)
    &&await thread.locator(`[data-message="${offlineId}"][data-message-kind="removed-by-moderation"]`).count()===0,
    'open disconnected Web thread still holds the missed source before automatic recovery');
  const refreshBefore=currentHistoryRefreshes;
  blockBrowserHub=false;
  check(await until(()=>browserSockets.length>socketCountBefore,20000),'WASM automatically opens a fresh actual server-connected hub socket');
  check(await until(()=>currentHistoryRefreshes>refreshBefore,20000),'automatic Reconnected handler reads current history through the actual BFF');
  await thread.locator(`[data-message="${offlineId}"][data-message-kind="removed-by-moderation"]`).waitFor({timeout:20000});
  const recovered=await thread.locator(`[data-message="${offlineId}"]`).textContent();
  check(recovered.includes('Removed by Swoovr.')&&!recovered.includes(offlineOriginal),'automatic reconnect replaces missed original with neutral marker');
  check(threadNavigations===0&&await thread.evaluate(()=>window.__contentReconnectDocument)===documentMarker,'recovery happens in the same open document without manual reload/navigation');
  await thread.screenshot({path:path.join(folder,'web-automatic-reconnect-removal-390.png'),fullPage:true});

  phase='restricted-retained-evidence-and-member-privacy';
  const evidencePath=removePath+'/'+result.body.id;
  const evidence=await call(st.adm,evidencePath);check(evidence.status===200&&evidence.body.text===original,'same-case authorized admin opens retained original');
  check((await call(st.adm,`/api/admin/cases/${foreignCase}/content-removals/${result.body.id}`)).status===404,'foreign case cannot open retained original');
  for(const token of [null,tokens.subject,st.leg,st.sup,st.subjectConflict,st.reporterConflict]){
   check((await call(token,evidencePath)).status===(token?403:401),'retained original refuses anonymous/member/legal/support/conflicted authority');
  }
  const sealed=await sql(`SELECT encode(sealed_snapshot,'base64') FROM content_removal WHERE id='${result.body.id}'`);
  check(sealed.length>40&&!Buffer.from(sealed.replaceAll('\n',''),'base64').includes(Buffer.from(original)),'retained original is encrypted in PostgreSQL');
  const standing=(await call(tokens.subject,'/api/me/standing')).body;
  check(standing.state==='active'&&standing.inForce.length===0&&standing.decisions.some(d=>d.kind==='profile-text-removed')&&standing.decisions.some(d=>d.kind==='message-removed'),'neutral standing records individual actions without whole-account sanction');
  const memberNotices=(await call(tokens.subject,'/api/me/notices')).body;
  check(memberNotices.filter(n=>n.kind==='contentRemoved').length===4,'one neutral member notice per individual removal');
  check(!JSON.stringify({standing,memberNotices}).includes(original)&&!JSON.stringify({standing,memberNotices}).includes('Fictional SSR single-field removal reason'),'member standing and notices expose no retained original or internal reason');
  check(await sql(`SELECT count(*) FROM sanctions WHERE account_id='${ids.subject}'`)==='0','individual removal created no account sanction');
  tokens.subject=await memberToken(browser,members.subject);
  const ticket=await call(tokens.subject,'/api/me/export',{method:'POST'});check(ticket.status===200,'fresh member session requests own export');
  const download=await fetch(api+'/'+ticket.body.path,{headers:{Authorization:'Bearer '+tokens.subject,'Swoovr-Version':'2026-10-28'}});
  const exported=await download.text();check(download.ok&&!exported.includes(original)&&!exported.includes(offlineOriginal)&&exported.includes('removed-by-moderation'),'own export omits retained original and preserves neutral removal state');
  console.log(`PASS: individual content removal acceptance (${passed} checks; owned API/PostgreSQL, actual Admin SSR/Web, automatic transport reconnect and two API hubs; no native OS UI). Screenshots: ${folder}`);
 }finally{for(const hub of hubs)await hub.close();for(const context of contexts)await context.close();await edge.close();}
})().catch(error=>{const frame=(error.stack||'').match(/content\.browser\.cjs:(\d+):\d+/);console.error('FAIL: individual content removal acceptance at '+phase+(error.safeLabel?' — '+error.message:' (transport/UI details withheld; script line '+(frame?.[1]||'unknown')+').'));process.exitCode=1;});
