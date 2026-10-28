// Removes every photo object that fictional suite accounts (`fictional.%@example.test`) left in the R2 bucket, for example
// after an aborted run of tests/media/photos.browser.cjs: a fictional legal officer releases those accounts' provisional
// `media-screening` holds (never anyone else's), their photos are marked removed, and `accounts purge` deletes the objects.
// Real accounts are never touched. Needs the same hosts and stack as the photos suite.
const assert = require('node:assert/strict');
const { execFile } = require('node:child_process');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { localEnv, createStaff, staffToken, rootAdminToken, sql, call } = require('../auth/local-tokens.cjs');

const fictional = `SELECT id FROM accounts WHERE email LIKE 'fictional.%@example.test'`;

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const holds = (await sql(`SELECT DISTINCT h.id FROM legal_holds h JOIN legal_hold_scopes s ON s.hold_id = h.id
      WHERE h.source = 'media-screening' AND h.status <> 'Released' AND s.target_id IN (${fictional})
      AND NOT EXISTS (SELECT 1 FROM legal_hold_scopes o WHERE o.hold_id = h.id AND o.target_id NOT IN (${fictional}))`)).split('\n').filter(Boolean);
    if (holds.length) {
      const root = await rootAdminToken(browser);
      const officer = await createStaff('photo-cleanup');
      assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: officer.email, role: 'legal' } })).status, 201);
      const token = await staffToken(browser, officer);
      await call(token, '/api/session');
      for (const hold of holds) {
        const r = await call(token, `/api/admin/holds/${hold}/release`, { method: 'POST', body: { reason: 'Fictional test photos cleaned up', overlapsChecked: true } });
        assert.equal(r.status, 200, `release ${hold}: ${r.status} ${r.body?.code ?? ''}`);
      }
    }
    await sql(`UPDATE media_items SET status = 'Removed', removed_at = coalesce(removed_at, now()), next_screen_at = NULL
      WHERE account_id IN (${fictional}) AND status IN ('Pending', 'Passed', 'Uncertain')`);
    // An aborted simulated-match suite may stop its extra host after sealing but before linking the hold.
    // These are synthetic fictional fixtures only. Active holds still prevent object deletion in accounts purge.
    await sql(`UPDATE media_items SET status = 'Removed', removed_at = coalesce(removed_at, now()), next_screen_at = NULL
      WHERE account_id IN (${fictional}) AND status = 'Blocked' AND hold_id IS NULL AND screener = 'simulated'
      AND case_id IN (SELECT id FROM moderation_cases WHERE subject_account_id IN (${fictional}))`);
    await sql(`DELETE FROM media_placements WHERE account_id IN (${fictional})`);
    const env = { ...process.env, DOTNET_ENVIRONMENT: 'Development',
      ConnectionStrings__Swoovr: `Host=127.0.0.1;Port=5433;Database=swoovr;Username=swoovr;Password=${await localEnv('SWOOVR_DB_PASSWORD')}`,
      Founders__Secrets__1: await localEnv('SWOOVR_FOUNDER_SECRET_V1'), MemberAdmin__ClientSecret: await localEnv('SWOOVR_MEMBERS_API_ADMIN_SECRET') ?? '',
      Media__R2__Endpoint: await localEnv('SWOOVR_R2_ENDPOINT'), Media__R2__Bucket: await localEnv('SWOOVR_R2_BUCKET'),
      Media__R2__AccessKeyId: await localEnv('SWOOVR_R2_ACCESS_KEY_ID'), Media__R2__SecretAccessKey: await localEnv('SWOOVR_R2_SECRET_ACCESS_KEY') };
    const out = await new Promise(resolve => execFile('dotnet', ['src/Swoovr.Api/bin/Debug/net10.0/Swoovr.Api.dll', 'accounts', 'purge'], { env }, (e, so, se) => resolve(so + se)));
    console.log(out.split('\n').filter(l => l.startsWith('Photos')).join('\n'));
    const left = await sql(`SELECT count(*) FROM media_items WHERE object_key IS NOT NULL AND account_id IN (${fictional})`);
    console.log(`Fictional photo objects left: ${left} (holds released: ${holds.length}).`);
    if (left !== '0') process.exitCode = 1;
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
