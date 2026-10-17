const { test } = require('node:test');
const assert = require('node:assert/strict');
const appearance = require('../../src/DatingApp.Web/wwwroot/appearance-core.js');
const schemes = ['electric','raspberry','cobalt','coral','orchid','solar'];
test('18 independent requested combinations resolve to 12 visuals', () => {
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
  assert.equal(visuals.size,12);
});
test('unknown or malformed preferences fail to the whole default pair', () => {
  for (const value of [null,{},[],{scheme:'electric'},{scheme:'unknown',mode:'dark'},{scheme:'solar',mode:'unknown'},'dark'])
    assert.deepEqual(appearance.normalize(value),{scheme:'electric',mode:'system'});
});
test('storage is versioned, minimal, and leaves malformed data untouched on read', () => {
  let text = '{broken';
  const storage = { getItem: () => text, setItem: (key,value) => { assert.equal(key,appearance.key); text=value; } };
  assert.deepEqual(appearance.load(storage).preference, appearance.defaults);
  assert.equal(text,'{broken');
  assert.equal(appearance.save(storage,{scheme:'solar',mode:'system'}),true);
  assert.deepEqual(JSON.parse(text),{version:1,scheme:'solar',mode:'system'});
  assert.deepEqual(appearance.load(storage).preference,{scheme:'solar',mode:'system'});
  text = JSON.stringify({version:99,scheme:'solar',mode:'dark'});
  assert.deepEqual(appearance.load(storage).preference,appearance.defaults);
});
test('storage denial reports failure without discarding the in-memory preference', () => {
  const denied = { getItem: () => { throw Error('denied'); }, setItem: () => { throw Error('denied'); } };
  assert.equal(appearance.load(denied).available,false);
  const choice = {scheme:'coral',mode:'dark'};
  assert.equal(appearance.save(denied,choice),false);
  assert.deepEqual(choice,{scheme:'coral',mode:'dark'});
  assert.deepEqual(appearance.normalize(appearance.defaults),{scheme:'electric',mode:'system'});
});
