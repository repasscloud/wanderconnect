import test from 'node:test';
import assert from 'node:assert/strict';
import { decide, POLICY_VERSION } from '../src/policy.mjs';
const clean = () => ({ malware:{status:'completed',classification:'clean',engineVersion:'test',signatureVersion:'test'}, nudenet:{status:'completed',detections:[]}, nsfw:{status:'completed',is_nsfw:false}, arachnid:{status:'completed',classification:'no-known-match'} });
test('complete independent signals pass; absent/disabled/failed mandatory signals never pass',()=>{
 assert.equal(decide(clean()).outcome,'passed'); assert.ok(POLICY_VERSION);
 for(const name of ['malware','nudenet','nsfw','arachnid']) for(const status of ['disabled','failed','not_requested',undefined]) { const r=clean();r[name]={status};assert.equal(decide(r).outcome,'pending'); }
});
test('known match dominates other engine failures; malware remains blocked',()=>{
 const r=clean();r.arachnid.classification='csam';r.nudenet.status='failed';assert.equal(decide(r).outcome,'blocked');
 r.arachnid.classification='harmful-abusive-material';assert.equal(decide(r).outcome,'blocked');
 const m=clean();m.malware.classification='infected';assert.equal(decide(m).outcome,'malware');
});
test('selected anatomy and NSFW refer; unselected anatomy does not',()=>{
 for(const label of ['FEMALE_GENITALIA_EXPOSED','MALE_GENITALIA_EXPOSED','ANUS_EXPOSED','FEMALE_BREAST_EXPOSED','BUTTOCKS_EXPOSED']) {let r=clean();r.nudenet.detections=[{class:label,score:0.4}];assert.equal(decide(r).outcome,'review');r.nudenet.detections[0].score=0.399;assert.equal(decide(r).outcome,'passed');}
 let r=clean();r.nsfw.is_nsfw=true;assert.equal(decide(r).outcome,'review');
 r=clean();r.nudenet.detections=[{class:'MALE_BREAST_EXPOSED',score:0.9}];assert.equal(decide(r).outcome,'passed');
});
test('malformed and unknown outputs fail closed and provider test refers',()=>{
 for(const score of [NaN,Infinity,-1,1.1,'0.8']) {const r=clean();r.nudenet.detections=[{class:'BUTTOCKS_EXPOSED',score}];assert.equal(decide(r).outcome,'pending');}
 const r=clean();r.nudenet.detections=[{class:'NEW_LABEL',score:0.1}];assert.equal(decide(r).outcome,'pending');
 r.nudenet.detections=[];r.nsfw.is_nsfw='false';assert.equal(decide(r).outcome,'pending');
 r.nsfw.is_nsfw=false;r.arachnid.classification='unknown';assert.equal(decide(r).outcome,'pending');
 r.arachnid.classification='test';assert.equal(decide(r).outcome,'review');
});
