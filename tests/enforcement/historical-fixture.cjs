// Fictional, internally consistent temporal fixtures only. No provider activity is claimed at these old dates.
const crypto=require('node:crypto'),assert=require('node:assert/strict');
async function releasedHistory(query,{accountId,staffId,email,key}){
 assert.match(accountId,/^[0-9a-f-]{36}$/);assert.match(staffId,/^[0-9a-f-]{36}$/);
  const dispositionCase=crypto.randomUUID(),dispositionBan=crypto.randomUUID(),dispositionTerm=crypto.randomUUID(),createdEvent=crypto.randomUUID(),revokedEvent=crypto.randomUUID();
  const fingerprint=crypto.createHmac('sha256',key).update('swoovr/banned-identifier/email\0'+email.toLowerCase()).digest();
  const snapshot=JSON.stringify({IdentifierHash:fingerprint.toString('base64'),KeyVersion:1,IdentifierType:'Email',CaseId:dispositionCase});
  await query(`BEGIN;
   UPDATE accounts SET created_at=now()-interval '4 years' WHERE id='${accountId}';
   INSERT INTO moderation_cases(id,subject_account_id,status,queue,priority,created_at,updated_at,closed_at,evidence_purged_at)
    VALUES('${dispositionCase}','${accountId}','Closed','Admin','High',now()-interval '4 years',now()-interval '3 years',now()-interval '3 years',now()-interval '2 years');
   INSERT INTO sanctions(id,account_id,case_id,kind,reason,starts_at,created_by,created_by_role,hide_from_discovery,no_new_requests,no_messages)
    VALUES('${dispositionTerm}','${accountId}','${dispositionCase}','Termination','Fictional old enforcement fixture',now()-interval '4 years','${staffId}','Admin',true,true,true);
   INSERT INTO banned_identifiers(id,identifier_type,identifier_hash,key_version,case_id,ban_reason_code,status,banned_at,ban_expires_at,last_reviewed_at,revoked_at,revoked_by,match_count)
    VALUES('${dispositionBan}','Email',decode('${fingerprint.toString('hex')}','hex'),1,'${dispositionCase}','permanent-account-ban','Revoked',now()-interval '4 years',now()-interval '3 years',now()-interval '3 years',now()-interval '3 years','${staffId}',0);
   INSERT INTO ban_transitions(id,banned_identifier_id,case_id,actor_account_id,at,action,old_status,new_status,old_expires_at,new_expires_at,reason,old_values,new_values)
    VALUES('${createdEvent}','${dispositionBan}','${dispositionCase}','${staffId}',now()-interval '4 years','created',NULL,'Active',NULL,NULL,'Fictional old creation','{}','${snapshot}'::jsonb),
     ('${revokedEvent}','${dispositionBan}','${dispositionCase}','${staffId}',now()-interval '3 years','revoked','Active','Revoked',NULL,now()-interval '3 years','Fictional old reviewed release','${snapshot}'::jsonb,'${snapshot}'::jsonb);
   INSERT INTO ban_case_notes(id,case_id,transition_id,author_account_id,at,text)
    VALUES('${crypto.randomUUID()}','${dispositionCase}','${createdEvent}','${staffId}',now()-interval '4 years','Fictional old creation'),
     ('${crypto.randomUUID()}','${dispositionCase}','${revokedEvent}','${staffId}',now()-interval '3 years','Fictional old reviewed release');
   COMMIT;`);
 return {dispositionCase,dispositionBan,dispositionTerm,fingerprint};
}
async function disposeFixture(query,banId){
 assert.match(banId,/^[0-9a-f-]{36}$/);
 // UI fixture only: use the actual bounded database authority against this fixture's own ID.
 // The application coordinator is covered independently in workflows.browser.cjs.
 await query(`BEGIN;
  INSERT INTO ban_dispositions(id,case_id,identifier_type,status,effective_end,history_until,disposed_at)
   SELECT id,case_id,identifier_type,status,revoked_at,((greatest(banned_at,last_reviewed_at,revoked_at) AT TIME ZONE 'UTC')+interval '24 months') AT TIME ZONE 'UTC',now()
    FROM banned_identifiers WHERE id='${banId}';
  DELETE FROM ban_case_notes WHERE transition_id IN(SELECT id FROM ban_transitions WHERE banned_identifier_id='${banId}');
  DELETE FROM ban_transitions WHERE banned_identifier_id='${banId}';
  DELETE FROM banned_identifiers WHERE id='${banId}'; COMMIT;`);
}
module.exports={releasedHistory,disposeFixture};
