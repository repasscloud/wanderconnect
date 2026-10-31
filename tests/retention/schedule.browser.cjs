// Local PostgreSQL/API operator acceptance. All people/content are fictional; SQL clocks affect only UUIDs created here.
// No provider delivery, external compliance or production restore claim.
const assert = require('node:assert/strict');
const { execFile } = require('node:child_process');
const { randomUUID } = require('node:crypto');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { createMember, memberToken, call, sql, cli, claims, web } = require('../auth/local-tokens.cjs');
(async () => {
  const edge = await require('../auth/managed-edge.cjs').managedEdge(chromium), browser = edge.browser;
  const cases = [], holds = []; let checks = 0, fixtureAccount;
  const equal = (actual, expected, label) => { assert.equal(actual, expected, label); checks++; };
  try {
    const member = await createMember('retention-clock');
    await cli('beta','add',member.email);
    const token = await memberToken(browser, member);
    equal((await call(token, '/api/session')).status, 200, 'real local session');
    const account = await sql(`SELECT id FROM accounts WHERE subject='${claims(token).sub}'`);
    assert.match(account, /^[0-9a-f-]{36}$/); fixtureAccount=account;
    const reporterToken = await memberToken(browser, await createMember('retention-reporter'));
    await call(reporterToken, '/api/session');
    const reporter = await sql(`SELECT id FROM accounts WHERE subject='${claims(reporterToken).sub}'`);
    const fixture = async ({ age = 13, serious = false, referral = false, legacy = false } = {}) => {
      const id = randomUUID(), report = randomUUID(); cases.push(id);
      await sql(`INSERT INTO moderation_cases (id,subject_account_id,status,queue,priority,created_at,updated_at,closed_at,system_flag,escalated_at)
        VALUES ('${id}','${account}','Closed','Support','Normal',now()-interval '${age} months',now()-interval '${age} months',now()-interval '${age} months',${serious ? "'media-known-content'" : 'NULL'},${referral ? "now()-interval '2 months'" : 'NULL'});
        INSERT INTO reports (id,reporter_account_id,subject_account_id,subject_type,subject_ref,category_id,priority,text,case_id,created_at)
        VALUES ('${report}','${reporter}','${account}','Profile','${account}','other','Normal','Fictional retained review text','${id}',now()-interval '${age} months');
        INSERT INTO case_evidence (id,case_id,report_id,kind,snapshot,captured_at,media_ids)
        VALUES ('${randomUUID()}','${id}','${report}','profile','${legacy ? 'unreadable-fixture' : '{}'}',now()-interval '${age} months',${legacy ? 'NULL' : "'{}'::uuid[]"});`);
      return { id, report };
    };
    const evidence = id => sql(`SELECT count(*) FROM case_evidence WHERE case_id='${id}'`);
    const recent = await fixture({ age: 11 }); await cli('retention','purge','--case',recent.id);
    equal(await evidence(recent.id),'1','ordinary content before minimum');
    const ordinary = await fixture(); await cli('retention','purge','--case',ordinary.id);
    equal(await evidence(ordinary.id),'0','ordinary content after minimum');
    equal(await sql(`SELECT text IS NULL FROM reports WHERE id='${ordinary.report}'`),'t','private report text expired');
    equal(await sql(`SELECT count(*) FROM moderation_cases WHERE id='${ordinary.id}'`),'1','minimal compliance record remains');
    await cli('retention','purge','--case',ordinary.id);
    equal(await evidence(ordinary.id),'0','repeat disposal idempotent');
    const serious = await fixture({ serious:true }); await cli('retention','purge','--case',serious.id);
    equal(await evidence(serious.id),'1','serious evidence exceeds ordinary minimum');
    const referred = await fixture({ age:25, referral:true }); await cli('retention','purge','--case',referred.id);
    equal(await evidence(referred.id),'1','latest referral restarts minimum');
    const old = await fixture({ age:25 }); await cli('retention','purge','--case',old.id);
    equal(await sql(`SELECT count(*) FROM moderation_cases WHERE id='${old.id}'`),'0','expired compliance record removed');
    const unknown = await fixture({ legacy:true }); await cli('retention','purge','--case',unknown.id);
    equal(await evidence(unknown.id),'1','unreadable legacy evidence fails closed');
    const covered = await fixture({ age:25 });
    for(let n=0;n<2;n++) {
      const id=randomUUID();holds.push(id);
      await sql(`INSERT INTO legal_holds (id,status,reason,source,case_id,placed_by_role,placed_at,review_due,retention_case_ids,related_account_ids)
        VALUES ('${id}','Active','Fictional retention overlap','staff','${covered.id}','Legal',now(),now()+interval '1 day',ARRAY['${covered.id}']::uuid[],ARRAY['${account}']::uuid[]);
        INSERT INTO legal_hold_scopes (hold_id,kind,target_id) VALUES ('${id}','Account','${account}');`);
    }
    await cli('retention','purge','--case',covered.id); equal(await evidence(covered.id),'1','active holds override expiry');
    await sql(`UPDATE legal_holds SET status='Released',released_at=now() WHERE id='${holds[0]}'`);
    await cli('retention','purge','--case',covered.id); equal(await evidence(covered.id),'1','overlapping hold still protects');
    await sql(`UPDATE legal_holds SET status='Released',released_at=now() WHERE id='${holds[1]}'`);
    await cli('retention','purge','--case',covered.id); equal(await evidence(covered.id),'0','release resumes expired schedule');
    // More than a full old retained batch must not starve later eligible work.
    const batch=await sql(`WITH rows AS MATERIALIZED (SELECT gen_random_uuid() AS cid,gen_random_uuid() AS rid FROM generate_series(1,1001)),
      made_cases AS (INSERT INTO moderation_cases(id,subject_account_id,status,queue,priority,created_at,updated_at,closed_at)
        SELECT cid,'${account}','Closed','Support','Normal',now()-interval '40 months',now()-interval '40 months',now()-interval '40 months' FROM rows RETURNING id),
      made_reports AS (INSERT INTO reports(id,reporter_account_id,subject_account_id,subject_type,subject_ref,category_id,priority,text,case_id,created_at)
        SELECT rows.rid,'${reporter}','${account}','Profile','${account}','other','Normal','Fictional legacy batch',rows.cid,now()-interval '40 months'
        FROM rows JOIN made_cases c ON c.id=rows.cid RETURNING id,case_id),
      made_evidence AS (INSERT INTO case_evidence(id,case_id,report_id,kind,snapshot,captured_at)
        SELECT gen_random_uuid(),case_id,id,'profile','unreadable-fixture',now()-interval '40 months' FROM made_reports RETURNING case_id)
      SELECT case_id FROM made_evidence`);
    const batchIds=batch.split('\n');cases.push(...batchIds);equal(batchIds.length,1001,'retained prefix exceeds old batch limit');
    const behind=randomUUID();cases.push(behind);
    await sql(`INSERT INTO moderation_cases(id,subject_account_id,status,queue,priority,created_at,updated_at,closed_at)
      VALUES ('${behind}','${account}','Closed','Support','Normal',now()-interval '25 months',now()-interval '25 months',now()-interval '25 months')`);
    await cli('retention','purge','--account',account);
    equal(await sql(`SELECT count(*) FROM moderation_cases WHERE id='${behind}'`),'0','eligible case behind retained batch is disposed');
    equal(await sql(`SELECT count(*) FROM moderation_cases WHERE id IN (${batchIds.map(x=>`'${x}'`).join(',')})`),'1001','uncertain batch still protected');
    // A concurrent coverage writer wins before a waiting purge rechecks authority. Actual PostgreSQL sessions.
    const concurrent = await fixture({ age:25 }), concurrentHold=randomUUID(); holds.push(concurrentHold);
    const tag='swoovr-retention-'+randomUUID();
    const writer = new Promise((resolve,reject)=>execFile('docker',['exec','swoovr-local-postgres-1','psql','-U','swoovr','-d',process.env.SWOOVR_TEST_APP_DATABASE||'swoovr','-v','ON_ERROR_STOP=1','-c',`
      SET application_name='${tag}'; SELECT pg_advisory_lock(1347568979,0); SELECT pg_sleep(3);
      INSERT INTO legal_holds (id,status,reason,source,case_id,placed_by_role,placed_at,review_due,retention_case_ids,related_account_ids)
      VALUES ('${concurrentHold}','Active','Fictional concurrent retention hold','staff','${concurrent.id}','Legal',now(),now()+interval '1 day',ARRAY['${concurrent.id}']::uuid[],ARRAY['${account}']::uuid[]);
      INSERT INTO legal_hold_scopes (hold_id,kind,target_id) VALUES ('${concurrentHold}','Account','${account}');
      SELECT pg_advisory_unlock(1347568979,0);`],{timeout:20000},(error,stdout,stderr)=>error?reject(new Error(stderr||error.message)):resolve()));
    let locked=false;
    for(let n=0;n<50;n++) {
      locked=await sql(`SELECT EXISTS(SELECT 1 FROM pg_locks l JOIN pg_stat_activity a ON a.pid=l.pid WHERE a.application_name='${tag}' AND l.locktype='advisory' AND l.granted)`) === 't';
      if(locked) break; await new Promise(resolve=>setTimeout(resolve,25));
    }
    assert.ok(locked,'fixture acquired exclusive preservation authority');checks++;
    await cli('retention','purge','--case',concurrent.id); await writer;
    equal(await evidence(concurrent.id),'1','purge observes concurrently committed hold');
    // Simulate an interrupted finalization marker only on this fixture; no provider deletion is invoked.
    await sql(`UPDATE accounts SET status='PendingDeletion',deletion_requested_at=now(),deletion_due_at=now(),deletion_finalizing_at=now() WHERE id='${account}'`);
    equal((await call(token,'/api/me/deletion',{method:'DELETE'})).status,409,'finalization cannot be cancelled');
    equal((await call(token,'/api/me/deletion/now',{method:'POST'})).status,409,'finalizing cannot be restarted by member');
    equal((await call(token,'/api/me/deletion')).body.finalizingAt != null,true,'finalization is explicit to clients');
    const context=await browser.newContext({viewport:{width:390,height:844}}),page=await context.newPage();
    await page.goto(web+'/account/sign-in?intro=done');
    await page.fill('#username',member.email);await page.fill('#password',member.password);
    await Promise.all([page.waitForURL(web+'/account'),page.click('#kc-login')]);
    await page.goto(web+'/account/data');await page.waitForSelector('[data-deletion-finalizing]');
    equal(await page.locator('[data-cancel-deletion], [data-delete-now]').count(),0,'no misleading recovery buttons during finalization');
    equal(await page.getByRole('link',{name:'Contact Support',exact:true}).getAttribute('href'),'https://swoovr.com/support','approved support link');
    equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true,'finalization fits narrow viewport');
    if(process.env.SWOOVR_SCREENSHOTS) await page.screenshot({path:process.env.SWOOVR_SCREENSHOTS+'/web-deletion-finalizing.png',fullPage:true});
    await context.close();
    await sql(`UPDATE accounts SET status='Active',deletion_requested_at=NULL,deletion_due_at=NULL,deletion_finalizing_at=NULL WHERE id='${account}'`);
    console.log(`PASS: retention schedule (${checks} checks; local PostgreSQL, fictional clocks, no external provider delivery).`);
  } finally {
    if(fixtureAccount) await sql(`UPDATE accounts SET status='Active',deletion_requested_at=NULL,deletion_due_at=NULL,deletion_finalizing_at=NULL WHERE id='${fixtureAccount}' AND deletion_finalizing_at IS NOT NULL`);
    if(holds.length) await sql(`DELETE FROM legal_hold_scopes WHERE hold_id IN (${holds.map(x=>`'${x}'`).join(',')}); DELETE FROM legal_holds WHERE id IN (${holds.map(x=>`'${x}'`).join(',')});`);
    if(cases.length) await sql(`DELETE FROM moderation_cases WHERE id IN (${cases.map(x=>`'${x}'`).join(',')});`);
    await edge.close();
  }
})().catch(error=>{ console.error(error);process.exitCode=1; });
