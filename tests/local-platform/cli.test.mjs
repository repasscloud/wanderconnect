import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
const run = (...args) => spawnSync('bash', ['scripts/local-platform.sh', ...args], { encoding: 'utf8', env: { ...process.env, PATH: '/usr/bin:/bin' } });
test('destructive commands refuse before touching Docker without explicit acknowledgement', () => {
  for (const args of [['reset'], ['up', '--fresh']]) {
    const r = run(...args);
    assert.equal(r.status, 2);
    assert.match(r.stderr, /requires --yes/);
  }
});
test('help needs no daemon and invalid options do not perform actions', () => {
  assert.equal(run('help').status, 0);
  assert.equal(run('down', '--take-ports').status, 2);
  assert.equal(run('reset', '--yes', '--unknown').status, 2);
});

test('another checkout cannot reset a labelled installation', async () => {
  const { mkdtemp, mkdir, copyFile, writeFile, readFile } = await import('node:fs/promises');
  const { join } = await import('node:path');
  const { tmpdir } = await import('node:os');
  const dir = await mkdtemp(join(tmpdir(), 'swoovr-owner-'));
  await mkdir(join(dir, 'scripts'));
  await mkdir(join(dir, 'fake'));
  await copyFile('scripts/local-platform.sh', join(dir, 'scripts/local-platform.sh'));
  const log = join(dir, 'calls');
  await writeFile(join(dir, 'fake/docker'), `#!/bin/sh
printf '%s\\n' "$*" >> "$FAKE_DOCKER_LOG"
case "$1" in
  ps) echo abc123 ;;
  inspect) echo /a/different/checkout ;;
esac
exit 0
`, { mode: 0o755 });
  const r = spawnSync('bash', [join(dir, 'scripts/local-platform.sh'), 'reset', '--yes'], {
    encoding: 'utf8', env: { ...process.env, PATH: join(dir, 'fake') + ':/usr/bin:/bin', FAKE_DOCKER_LOG: log },
  });
  assert.equal(r.status, 1);
  assert.match(r.stderr, /another\/unlabelled checkout/);
  assert.doesNotMatch(await readFile(log, 'utf8'), /\bdown\b|\bstop\b|\brm\b/);
});

test('conflicting data-mode flags are rejected before Docker', () => {
  assert.equal(run('up', '--reuse', '--fresh', '--yes').status, 2);
  assert.equal(run('reset', '--reuse', '--yes').status, 2);
});

test('confirmed reset removes realm-bound staff fixtures but retains credentials and unrelated files', async () => {
  const { mkdtemp, mkdir, copyFile, writeFile, readFile, access, rm } = await import('node:fs/promises');
  const { join } = await import('node:path');
  const { tmpdir } = await import('node:os');
  const dir = await mkdtemp(join(tmpdir(), 'swoovr-reset-'));
  try {
    await mkdir(join(dir, 'scripts'));await mkdir(join(dir, 'fake'));await mkdir(join(dir, '.local/platform'), { recursive:true });
    await copyFile('scripts/local-platform.sh', join(dir, 'scripts/local-platform.sh'));
    await writeFile(join(dir, 'fake/docker'), '#!/bin/sh\nexit 0\n', { mode:0o755 });
    const state=join(dir,'.local/platform');
    const bound=['admin.json','staff-support.json','staff-trust-safety.json','identities.json','admin-subject','initialized','staff-roles.tsv','staff-roles.status'];
    for(const file of [...bound,'docker.env','unrelated.json'])await writeFile(join(state,file),'fictional owned fixture');
    const r=spawnSync('bash',[join(dir,'scripts/local-platform.sh'),'reset','--yes'],{encoding:'utf8',env:{...process.env,PATH:join(dir,'fake')+':/usr/bin:/bin'}});
    assert.equal(r.status,0,r.stderr);
    for(const file of bound)await assert.rejects(access(join(state,file)),{code:'ENOENT'},file+' must not refer to deleted subjects');
    assert.equal(await readFile(join(state,'docker.env'),'utf8'),'fictional owned fixture');
    assert.equal(await readFile(join(state,'unrelated.json'),'utf8'),'fictional owned fixture');
  } finally {await rm(dir,{recursive:true,force:true});}
});
