// Issue126: real local API/PostgreSQL/Mailpit/crypto, synthetic LocalMediaStore objects and labelled simulated screening.
// No known-content material, people/photos or remote provider calls. Never mutate shared .local configuration.
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const { execFile, spawn } = require('node:child_process');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, sleep, createMember, createStaff, cli, memberToken, staffToken, rootAdminToken, call, claims, matchReady, sql, localEnv, mailTo } = require('../auth/local-tokens.cjs');
const { synthetic, upload } = require('./synthetic-images.cjs');
const extra = 'http://127.0.0.1:5090';
async function stopExtra() {
  await new Promise(resolve => execFile('bash', ['-c', 'lsof -ti tcp:5090 -sTCP:LISTEN | xargs kill 2>/dev/null; true'], () => resolve()));
  for (let i=0;i<30;i++) { try { await fetch(extra+'/health/live'); await sleep(250); } catch { return; } }
}
async function startExtra(fault) {
  await stopExtra();
  await new Promise((resolve,reject) => {
    const p=spawn('bash',['.local/run-hosts.sh'],{env:{...process.env,SWOOVR_EXTRA_API_PORT:'5090',Phone__Required:'false',Accounts__ReauthSeconds:'45',Media__Screening__Simulate:'blocked',Media__Preservation__Fault:fault},stdio:['ignore','pipe','pipe']});
    p.on('exit',code=>code===0?resolve():reject(Error('Extra recovery API failed to start')));
  });
}
async function waitFor(check) { for(let i=0;i<120;i++){if(await check())return;await sleep(250);}throw Error('Recovery state did not arrive'); }
(async()=>{
  const rootDir=await fs.mkdtemp(path.join(os.tmpdir(),'swoovr-preservation-'));
  const browser=await chromium.launch({channel:'msedge',headless:true});
  const canvas=await browser.newPage(); const accounts=[]; let legal;
  let passed=0;const pass=()=>passed++;
  const priorStore=process.env.Media__Store, priorPath=process.env.Media__Local__Path;
  process.env.Media__Store='local';process.env.Media__Local__Path=rootDir;
  try {
    const root=await rootAdminToken(browser), officer=await createStaff('recovery-legal');
    assert.equal((await call(root,'/api/admin/invitations',{method:'POST',body:{email:officer.email,role:'legal'}})).status,201);
    legal=await staffToken(browser,officer);await call(legal,'/api/session');
    const primary=await localEnv('SWOOVR_ESCALATION_EMAIL');assert.ok(primary);
    const destinationCount=new Set([primary,await localEnv('SWOOVR_ESCALATION_BACKUP_EMAIL')].filter(Boolean).map(a=>a.trim().toLowerCase())).size;
    for(const fault of ['blocked-save','hold-create','object-sealed','capture-complete','capture-error','uncertain-alert','mail-unavailable','released-hold']) {
      const member=await createMember('recover-'+fault);await cli('beta','add',member.email);
      const token=await memberToken(browser,member);
      await call(token,'/api/me/profile',{method:'PUT',body:{displayName:'Fictional Recovery Tester',homeCityId:2078025}});await matchReady(token);
      const account=await sql(`SELECT id FROM accounts WHERE subject='${claims(token).sub}'`);accounts.push(account);
      await startExtra(fault==='released-hold'?'hold-create':['uncertain-alert','mail-unavailable'].includes(fault)?'capture-complete':fault);
      const response=await upload(extra+'/api/me/photos',await synthetic(canvas,{kind:'gradient',type:'image/jpeg',seed:100+passed}),{token});
      assert.equal(response.status,202,JSON.stringify(response.body));const id=response.body.id;pass();
      await waitFor(async()=>await sql(`SELECT count(*) FROM media_items WHERE id='${id}' AND preservation_pending_at IS NOT NULL AND preservation_error IS NOT NULL`)==='1');
      assert.equal(await sql(`SELECT status FROM media_items WHERE id='${id}'`),'Blocked');pass();
      const beforeKey=await sql(`SELECT object_key FROM media_items WHERE id='${id}'`);
      let originalCipher=null;
      if(beforeKey.startsWith('evidence/')) originalCipher=crypto.createHash('sha256').update(await fs.readFile(path.join(rootDir,beforeKey))).digest('hex');
      if(fault==='blocked-save') {
        assert.equal(await sql(`SELECT count(*) FROM legal_hold_scopes s JOIN legal_holds h ON h.id=s.hold_id WHERE s.target_id='${account}' AND h.status<>'Released'`),'0');pass();
      }
      const caseId=await sql(`SELECT case_id FROM media_items WHERE id='${id}'`);
      if(fault==='hold-create') {
        assert.equal(await sql(`SELECT count(*) FROM legal_holds WHERE case_id='${caseId}' AND source='media-screening'`),'1');
        assert.equal(await sql(`SELECT count(*) FROM media_items WHERE id='${id}' AND hold_id IS NULL`),'1');pass();
      }
      if(fault==='capture-error') {
        assert.equal(await sql(`SELECT preservation_error FROM media_items WHERE id='${id}'`),'capture-unavailable');
        await waitFor(async()=>(await mailTo(primary)).some(t=>t.includes(caseId)));
        await waitFor(async()=>await sql(`SELECT count(*) FROM media_escalation_deliveries WHERE media_id='${id}' AND status='accepted'`)===String(destinationCount));pass();
      }
      await stopExtra();
      if(fault==='blocked-save') {
        assert.equal((await call(token,'/api/me/deletion',{method:'POST',body:{confirm:true,immediately:true,deleteFounderToken:false}})).status,200);pass();
        // The global purge must use the real configured R2 store for all unrelated existing fixtures, never this local store.
        const savedStore=process.env.Media__Store;process.env.Media__Store='r2';
        try { await assert.rejects(cli('accounts','purge')); } finally { process.env.Media__Store=savedStore; }
        assert.equal(await sql(`SELECT status FROM accounts WHERE id='${account}'`),'PendingDeletion');
        assert.equal(await sql(`SELECT count(*) FROM member_profiles WHERE account_id='${account}'`),'1');pass();
      }
      if(fault==='released-hold') {
        const originalHold=await sql(`SELECT id FROM legal_holds WHERE case_id='${caseId}' AND source='media-screening'`);
        assert.equal((await call(legal,`/api/admin/holds/${originalHold}/release`,{method:'POST',body:{reason:'Fictional legal release before interrupted media linkage; do not recreate.',overlapsChecked:true}})).status,200);pass();
        await assert.rejects(cli('media','preserve',id));pass();
        assert.equal(await sql(`SELECT count(*) FROM legal_holds WHERE case_id='${caseId}' AND source='media-screening'`),'1');
        assert.equal(await sql(`SELECT hold_id FROM media_items WHERE id='${id}'`),originalHold);pass();
        assert.equal(await sql(`SELECT preservation_error FROM media_items WHERE id='${id}'`),'hold-released');
        assert.equal(await sql(`SELECT count(*) FROM media_items WHERE id='${id}' AND next_preservation_at IS NULL`),'1');pass();
        // A genuinely new upload in the same still-open case gets its own durable hold origin.
        await startExtra('capture-complete');
        const nextUpload=await upload(extra+'/api/me/photos',await synthetic(canvas,{kind:'gradient',type:'image/jpeg',seed:880}),{token});
        assert.equal(nextUpload.status,202);const nextId=nextUpload.body.id;pass();
        await waitFor(async()=>await sql(`SELECT preservation_error FROM media_items WHERE id='${nextId}'`)==='development-pause');
        await stopExtra();await cli('media','preserve',nextId);
        const nextHold=await sql(`SELECT hold_id FROM media_items WHERE id='${nextId}'`);
        assert.notEqual(nextHold,originalHold);assert.equal(await sql(`SELECT source_media_id FROM legal_holds WHERE id='${nextHold}'`),nextId);pass();
        assert.equal(await sql(`SELECT count(*) FROM moderation_cases WHERE subject_account_id='${account}'`),'1');
        assert.equal(await sql(`SELECT count(*) FROM legal_holds WHERE case_id='${caseId}' AND source='media-screening'`),'2');pass();
        assert.equal(await sql(`SELECT count(*) FROM media_items WHERE id='${nextId}' AND preservation_completed_at IS NOT NULL`),'1');pass();
        continue;
      }
      if(fault==='mail-unavailable') {
        const previousPort=process.env.Email__Smtp__Port;process.env.Email__Smtp__Port='1';
        try { await assert.rejects(cli('media','preserve',id)); } finally { if(previousPort===undefined)delete process.env.Email__Smtp__Port;else process.env.Email__Smtp__Port=previousPort; }
        assert.equal(await sql(`SELECT status FROM media_escalation_deliveries WHERE media_id='${id}' AND slot='primary'`),'retry');pass();
        assert.equal((await mailTo(primary)).filter(t=>t.includes(caseId)).length,0);pass();
      }
      if(fault==='uncertain-alert') {
        await sql(`INSERT INTO media_escalation_deliveries(media_id,slot,status,attempts,updated_at) VALUES('${id}','primary','attempting',1,now()-interval '10 minutes')`);
        await assert.rejects(cli('media','preserve',id));pass();
        assert.equal(await sql(`SELECT status FROM media_escalation_deliveries WHERE media_id='${id}' AND slot='primary'`),'unknown');
        assert.equal((await mailTo(primary)).filter(t=>t.includes(caseId)).length,0);pass();
        await assert.rejects(cli('media','preserve',id));
        assert.equal((await mailTo(primary)).filter(t=>t.includes(caseId)).length,0);pass();
        const note=path.join(rootDir,'resolution-note.txt');await fs.writeFile(note,'Fictional test: the staged interrupted intent sent no email; operator verified non-acceptance.');
        await cli('media','alerts','resolve',id,'primary','retry','--note-file',note);pass();
      }
      await Promise.all([cli('media','preserve',id),cli('media','preserve',id)]);
      const row=(await sql(`SELECT object_key,case_id,hold_id,screen_attempts FROM media_items WHERE id='${id}'`)).split('|');
      assert.ok(row[0].startsWith('evidence/'));assert.ok(row[1]&&row[2]);assert.equal(row[3],'1','never rescan sealed ciphertext');pass();
      const cipherHash=crypto.createHash('sha256').update(await fs.readFile(path.join(rootDir,row[0]))).digest('hex');
      if(originalCipher) assert.equal(cipherHash,originalCipher,'reuse exact sealed bytes');pass();
      assert.equal(await sql(`SELECT count(*) FROM moderation_cases WHERE subject_account_id='${account}'`),'1');
      assert.equal(await sql(`SELECT count(*) FROM legal_holds WHERE case_id='${row[1]}' AND source='media-screening'`),'1');pass();
      assert.ok(Number(await sql(`SELECT count(*) FROM evidence_items WHERE hold_id='${row[2]}' AND kind='media-item' AND record_id='${id}'`))>=1);pass();
      assert.equal((await mailTo(primary)).filter(t=>t.includes(row[1])).length,1,'one accepted logical alert per recipient');pass();
      await cli('media','preserve',id);
      assert.equal(crypto.createHash('sha256').update(await fs.readFile(path.join(rootDir,row[0]))).digest('hex'),cipherHash);
      assert.equal((await mailTo(primary)).filter(t=>t.includes(row[1])).length,1);pass();
      assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action='media.preservation-completed' AND target_id='${id}'`),'1');pass();
      assert.equal(await sql(`SELECT count(*) FROM media_items WHERE id='${id}' AND preservation_completed_at IS NOT NULL`),'1');pass();
      if(fault==='blocked-save') {
        const fresh=await memberToken(browser,member);
        assert.equal((await call(fresh,'/api/me/deletion',{method:'DELETE'})).status,200);pass();
      }
    }
    console.log(`PASS: interrupted blocked-media recovery (${passed} checks; synthetic local objects, no real known-content assurance).`);
  } finally {
    await stopExtra();
    if(legal&&accounts.length) {
      const ids=accounts.map(a=>`'${a}'`).join(',');
      const holds=await sql(`SELECT DISTINCT h.id FROM legal_holds h JOIN legal_hold_scopes s ON s.hold_id=h.id WHERE s.target_id IN (${ids}) AND h.status<>'Released'`);
      for(const hold of holds.split('\n').filter(Boolean)) {
        const released=await call(legal,`/api/admin/holds/${hold}/release`,{method:'POST',body:{reason:'Fictional local recovery acceptance finished; no real material.',overlapsChecked:true}});
        assert.equal(released.status,200,'fixture hold must release before local object cleanup');
      }
      // Only this suite's synthetic local objects, after all holds released. No R2 writes/deletes occur here.
      await sql(`DELETE FROM media_items WHERE account_id IN (${ids})`);
    }
    if(priorStore===undefined)delete process.env.Media__Store;else process.env.Media__Store=priorStore;
    if(priorPath===undefined)delete process.env.Media__Local__Path;else process.env.Media__Local__Path=priorPath;
    await fs.rm(rootDir,{recursive:true,force:true});await browser.close();
  }
})().catch(e=>{console.error(e);process.exit(1)});
