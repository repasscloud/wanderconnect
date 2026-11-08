/** Automatic image policy. Changes require tests, documented version and Worker release. */
export const POLICY_VERSION = 'swoovr-images-2026-11-08-v1';
const selected = new Set(['FEMALE_GENITALIA_EXPOSED','MALE_GENITALIA_EXPOSED','ANUS_EXPOSED','FEMALE_BREAST_EXPOSED','BUTTOCKS_EXPOSED']);
const known = new Set([...selected,'MALE_BREAST_EXPOSED','FEMALE_GENITALIA_COVERED','FEMALE_BREAST_COVERED','BUTTOCKS_COVERED','ANUS_COVERED','BELLY_EXPOSED','BELLY_COVERED','ARMPITS_EXPOSED','ARMPITS_COVERED','FEET_EXPOSED','FEET_COVERED','FACE_FEMALE','FACE_MALE']);
const verdict = (outcome,reason) => ({outcome,reason,policyVersion:POLICY_VERSION});
export function decide(results) {
 const r=results??{};
 if(r.arachnid?.status==='completed' && ['csam','harmful-abusive-material'].includes(r.arachnid.classification)) return verdict('blocked','known-content-match');
 if(r.malware?.status==='completed' && r.malware.classification==='infected') return verdict('malware','malware-detected');
 if(r.malware?.status==='unsupported') return verdict('unsupported','malware-scan-limit');
 for(const name of ['malware','nudenet','nsfw','arachnid']) if(r[name]?.status!=='completed') return verdict('pending',`${name}-unchecked`);
 if(r.malware.classification!=='clean' || !r.malware.engineVersion || !r.malware.signatureVersion) return verdict('pending','malware-result-invalid');
 if(!['no-known-match','test'].includes(r.arachnid.classification)) return verdict('pending','unknown-classification');
 if(typeof r.nsfw.is_nsfw!=='boolean') return verdict('pending','nsfw-result-invalid');
 if(!Array.isArray(r.nudenet.detections) || r.nudenet.detections.length>1000 || r.nudenet.detections.some(d=>!d || !known.has(d.class) || typeof d.score!=='number' || !Number.isFinite(d.score) || d.score<0 || d.score>1)) return verdict('pending','nudity-result-invalid');
 if(r.arachnid.classification==='test') return verdict('review','known-content-test');
 if(r.nudenet.detections.some(d=>selected.has(d.class)&&d.score>=0.40)) return verdict('review','nudity-signal');
 if(r.nsfw.is_nsfw) return verdict('review','nsfw-signal');
 return verdict('passed','no-signal');
}
