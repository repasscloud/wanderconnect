// Owned local media worker, synthetic pixels and explicitly SIMULATED screening; no external provider proof.
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
const {managedEdge}=require('../auth/managed-edge.cjs');
const {api,createMember,memberToken,rootAdminToken,cli,call,sql,claims,sleep}=require('../auth/local-tokens.cjs');
const {synthetic,upload}=require('./synthetic-images.cjs');
if(!process.env.SWOOVR_TEST_APP_DATABASE||!['uncertain','blocked'].includes(process.env.Media__Screening__Simulate))throw Error('Use owned runner with explicitly simulated screening.');
let checks=0;const check=(ok,label)=>{if(!ok)throw Error(label);checks++;};
const wait=async test=>{for(let n=0;n<150;n++){if(await test())return;await sleep(200);}throw Error('Owned media transition timeout');};
(async()=>{const edge=await managedEdge(chromium),browser=edge.browser;let context;
try{
 const root=await rootAdminToken(browser),member=await createMember('media-reporting');await cli('beta','add',member.email);const token=await memberToken(browser,member);
 check((await call(token,'/api/me/profile',{method:'PUT',body:{displayName:'Fictional media reporting',homeCityId:2078025}})).status===200,'profile saved');
 context=await browser.newContext();const canvas=await context.newPage();const response=await upload(api+'/api/me/photos',await synthetic(canvas,{kind:'gradient',type:'image/jpeg',seed:807}),{token});
 check(response.status===202,'synthetic image accepted to quarantine');const id=response.body.id;
 const mode=process.env.Media__Screening__Simulate,status=mode==='blocked'?'Blocked':'Uncertain';
 await wait(async()=>await sql(`SELECT status FROM media_items WHERE id='${id}'`)===status);
 check(await sql(`SELECT completed_inspections FROM moderation_inspection_totals WHERE definition='image-screening-unversioned' AND environment='local-synthetic'`)==='1','actual worker records one inspection');
 if(mode==='uncertain'){
  check(await sql(`SELECT created_candidates FROM moderation_inspection_totals WHERE definition='image-screening-unversioned' AND environment='local-synthetic'`)==='1','uncertain candidate recorded');
  check(await sql(`SELECT count(*) FROM moderation_transparency_facts WHERE source_id='${id}'`)==='0','uncertainty is not an enforcement action');
  check((await call(root,`/api/admin/media/${id}/decision`,{method:'POST',body:{decision:'reject',reason:'Fictional explicit human review'}})).status===200,'actual human rejection succeeds');
  check(await sql(`SELECT enforcement_mechanism||'/'||detection_mechanism FROM moderation_transparency_facts WHERE source_id='${id}'`)==='human/image-screening','human rejection retains actual image detection');
 }else{
  await wait(async()=>await sql(`SELECT count(*) FROM media_items WHERE id='${id}' AND hold_id IS NOT NULL`)==='1');
  check(await sql(`SELECT enforcement_mechanism||'/'||detection_mechanism FROM moderation_transparency_facts WHERE source_id='${id}'`)==='automated-screening/image-screening','automatic blocked transition receipt');
  check(await sql(`SELECT created_candidates FROM moderation_inspection_totals WHERE definition='image-screening-unversioned' AND environment='local-synthetic'`)==='0','automatic refusal is not a human-review candidate');
  check(await sql(`SELECT count(*) FROM legal_holds WHERE source_media_id='${id}'`)==='1','blocked recovery retains a single durable hold');
 }
 check(await sql(`SELECT count(*) FROM moderation_transparency_facts WHERE source_id='${id}' AND environment='local-synthetic' AND home_country_at_action='unknown'`)==='1','one minimal local receipt with no invented residence');
 check((await call(root,`/api/admin/media/${id}/decision`,{method:'POST',body:{decision:'reject',reason:'Fictional retry'}})).status===404,'terminal decision retry refused');
 check(await sql(`SELECT count(*) FROM moderation_transparency_facts WHERE source_id='${id}'`)==='1','retry cannot duplicate receipt');
 console.log(`PASS: media reporting ${mode} (${checks} checks; owned worker/API/PostgreSQL, synthetic pixels, SIMULATED screening).`);
}finally{if(context)await context.close();await edge.close();}})().catch(e=>{console.error(e.message);process.exitCode=1;});
