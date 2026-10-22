const { test } = require('node:test');
const assert = require('node:assert/strict');
const appearance = require('../../src/Swoovr.Web/wwwroot/appearance-core.js');
const schemes = ['standard','lounge','pride','trans','union','violet','coral','eucalyptus'];
test('24 independent requested combinations resolve to 16 visuals', () => {
  const visuals = new Set();
  for (const scheme of schemes) for (const mode of ['system','light','dark']) {
    const choice = { scheme, mode };
    assert.deepEqual(appearance.normalize(choice), choice);
    for (const dark of [true,false]) {
      const result = appearance.resolve(choice,dark);
      assert.equal(result.scheme,scheme);
      assert.equal(result.mode, mode === 'system' ? dark ? 'dark' : 'light' : mode);
      visuals.add(result.scheme+':'+result.mode);
    }
  }
  assert.equal(visuals.size,16);
});
test('unknown or malformed preferences fail to the whole default pair', () => {
  for (const value of [null,{},[],{scheme:'standard'},{scheme:'unknown',mode:'dark'},{scheme:'eucalyptus',mode:'unknown'},{scheme:'electric',mode:'dark'},{scheme:'solar',mode:'light'},'dark'])
    assert.deepEqual(appearance.normalize(value),{scheme:'standard',mode:'system'});
});
test('storage is versioned, minimal, and leaves malformed data untouched on read', () => {
  let text = '{broken';
  const storage = { getItem: () => text, setItem: (key,value) => { assert.equal(key,appearance.key); text=value; } };
  assert.deepEqual(appearance.load(storage).preference, appearance.defaults);
  assert.equal(text,'{broken');
  assert.equal(appearance.save(storage,{scheme:'eucalyptus',mode:'system'}),true);
  assert.deepEqual(JSON.parse(text),{version:2,scheme:'eucalyptus',mode:'system'});
  assert.deepEqual(appearance.load(storage).preference,{scheme:'eucalyptus',mode:'system'});
  text = JSON.stringify({version:99,scheme:'eucalyptus',mode:'dark'});
  assert.deepEqual(appearance.load(storage).preference,appearance.defaults);
});
test('storage denial reports failure without discarding the in-memory preference', () => {
  const denied = { getItem: () => { throw Error('denied'); }, setItem: () => { throw Error('denied'); } };
  assert.equal(appearance.load(denied).available,false);
  const choice = {scheme:'coral',mode:'dark'};
  assert.equal(appearance.save(denied,choice),false);
  assert.deepEqual(choice,{scheme:'coral',mode:'dark'});
  assert.deepEqual(appearance.normalize(appearance.defaults),{scheme:'standard',mode:'system'});
});
test('schema v1 choices migrate: Electric to Violet, Coral stays, other retired schemes to Standard', () => {
  let text;
  const storage = { getItem: () => text, setItem: () => assert.fail('load must not write') };
  for (const [scheme, expected] of [['electric','violet'],['coral','coral'],['raspberry','standard'],['cobalt','standard'],['orchid','standard'],['solar','standard']]) {
    text = JSON.stringify({version:1,scheme,mode:'dark'});
    assert.deepEqual(appearance.load(storage).preference,{scheme:expected,mode:'dark'});
  }
  text = JSON.stringify({version:1,scheme:'electric',mode:'unknown'});
  assert.deepEqual(appearance.load(storage).preference,appearance.defaults);
  text = JSON.stringify({version:2,scheme:'electric',mode:'dark'});
  assert.deepEqual(appearance.load(storage).preference,appearance.defaults);
});
