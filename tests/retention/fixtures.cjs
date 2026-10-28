// Local acceptance cleanup only. Callers pass UUIDs they created in this run, never arbitrary account searches.
// Advance only positively scoped fictional reviews after releasing every hold; actual production expiry is never bypassed.
const assert = require('node:assert/strict');
const {sql,cli}=require('../auth/local-tokens.cjs');
async function expireFictionalReviews(accounts) {
  assert.ok(accounts.length);for(const id of accounts)assert.match(id,/^[0-9a-f-]{36}$/);
  const list=accounts.map(id=>`'${id}'`).join(',');
  assert.equal(await sql(`SELECT count(*) FROM accounts WHERE id IN (${list}) AND NOT
    (email LIKE 'fictional.%@example.test' OR email IS NULL AND status='Deleted')`),'0','only caller-owned fictional identities');
  const cases=(await sql(`SELECT id FROM moderation_cases c WHERE subject_account_id IN (${list})
    AND NOT EXISTS(SELECT 1 FROM reports r WHERE r.case_id=c.id AND (r.reporter_account_id NOT IN (${list}) OR r.subject_account_id NOT IN (${list})))`)).split('\n').filter(Boolean);
  if(!cases.length)return;
  const ids=cases.map(id=>`'${id}'`).join(',');
  const linked=`h.case_id IN (${ids}) OR h.retention_case_ids && ARRAY[${ids}]::uuid[]`;
  const holds=(await sql(`SELECT id FROM legal_holds h WHERE ${linked}`)).split('\n').filter(Boolean);
  if(holds.length){
    const hs=holds.map(id=>`'${id}'`).join(',');
    assert.equal(await sql(`SELECT count(*) FROM legal_holds h WHERE id IN (${hs}) AND
      (status<>'Released' OR le_case_id IS NOT NULL OR retention_case_ids IS NULL OR related_account_ids IS NULL
       OR NOT related_account_ids <@ ARRAY[${list}]::uuid[] OR NOT retention_case_ids <@ ARRAY[${ids}]::uuid[]
       OR EXISTS(SELECT 1 FROM legal_hold_scopes s WHERE s.hold_id=h.id AND s.kind='Account' AND s.target_id NOT IN (${list}))
       OR EXISTS(SELECT 1 FROM disclosures d WHERE d.hold_id=h.id)
       OR EXISTS(SELECT 1 FROM evidence_exports e WHERE e.hold_id=h.id AND e.le_case_id IS NOT NULL))`),'0','no shared/active/unknown/police obligation may be shortened');
    await sql(`UPDATE evidence_exports SET created_at=now()-interval '30 months' WHERE hold_id IN (${hs})`);
  }
  assert.equal(await sql(`SELECT count(*) FROM appeals a JOIN sanctions s ON s.id=a.sanction_id WHERE s.case_id IN (${ids}) AND a.status='Pending'`),'0','never dispose an unresolved appeal fixture');
  await sql(`UPDATE moderation_cases SET status='Closed',closed_at=now()-interval '30 months',escalated_at=NULL WHERE id IN (${ids});
    UPDATE appeals SET decided_at=now()-interval '30 months' WHERE sanction_id IN (SELECT id FROM sanctions WHERE case_id IN (${ids})) AND decided_at IS NOT NULL`);
  // Related copies can span several fixture cases. A second targeted pass finishes record disposal after copy expiry.
  for(let pass=0;pass<2;pass++)for(const id of cases)await cli('retention','purge','--case',id);
}
module.exports={expireFictionalReviews};
