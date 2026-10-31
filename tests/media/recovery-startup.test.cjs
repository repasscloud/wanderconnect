const test = require('node:test');
const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const path = require('node:path');
for (const fault of ['blocked-save', 'hold-create', 'object-sealed', 'capture-complete', 'capture-error'])
  test('Production rejects preservation fault ' + fault, () => {
    const r = spawnSync('dotnet', ['bin/Debug/net10.0/Swoovr.Api.dll'], { cwd: path.resolve(__dirname, '../../src/Swoovr.Api'), timeout: 10000, encoding: 'utf8',
      env: { PATH: process.env.PATH, HOME: process.env.HOME, DOTNET_ENVIRONMENT: 'Production', ASPNETCORE_ENVIRONMENT: 'Production', Media__Preservation__Fault: fault } });
    assert.notEqual(r.status, 0);
    assert.ok((r.stdout + r.stderr).includes('Media preservation interruption hooks are Development only.'));
  });

for (const stage of ['before-lease', 'after-read'])
  test('Production rejects publication barrier ' + stage, () => {
    const r = spawnSync('dotnet', ['bin/Debug/net10.0/Swoovr.Api.dll'], { cwd: path.resolve(__dirname, '../../src/Swoovr.Api'), timeout: 10000, encoding: 'utf8',
      env: { PATH: process.env.PATH, HOME: process.env.HOME, DOTNET_ENVIRONMENT: 'Production', ASPNETCORE_ENVIRONMENT: 'Production', Media__Publication__BarrierDirectory: '/tmp/swoovr-fictional-barriers', Media__Publication__BarrierStage: stage } });
    assert.notEqual(r.status, 0);
    assert.ok((r.stdout + r.stderr).includes('Media publication barriers are Development only.'));
  });
