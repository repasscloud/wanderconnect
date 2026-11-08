import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { validateManifest } from '../src/processing.mjs';
import { sha256 } from '../src/shared.mjs';
const fixture=process.env.PROCESSOR_RESPONSE_FIXTURE;
test('actual FastAPI multipart response matches Worker manifest and binary contract',{skip:!fixture},async()=>{
 const bytes=await readFile(fixture+'.bin');const meta=JSON.parse(await readFile(fixture+'.json','utf8'));
 const form=await new Response(bytes,{headers:{'content-type':meta.contentType}}).formData();
 const manifest=JSON.parse(form.get('manifest'));
 const decision=validateManifest(manifest,manifest,manifest.sourceSha256,form);
 assert.equal(decision.outcome,'passed');
 assert.deepEqual(manifest.variants.map(v=>v.name).sort(),['display','thumbnail']);
 for(const variant of manifest.variants){const part=form.get(variant.name);assert.equal(part.size,variant.bytes);assert.equal(part.type,variant.contentType);assert.equal(await sha256(await part.arrayBuffer()),variant.sha256);}
});
