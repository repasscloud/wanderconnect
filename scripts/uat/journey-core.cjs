// Same member-facing contract for guided UAT and owned local acceptance. No credentials or IDs logged.
const {randomUUID}=require('node:crypto');
function ensure(value,step){if(!value)throw new Error('UAT smoke failed at '+step+'; inspect the private browser, never print response bodies.');}
async function journeyCore(a,b,record=()=>{}){
 const call=async(client,path,method='GET',body)=>client(path,{method,body});
 const expect=async(client,path,status,method='GET',body)=>{const r=await call(client,path,method,body);ensure(r.status===status,path.split('?')[0].replace(/[0-9a-f-]{36}/g,'[id]'));return r.body;};
 const profiles=[];
 for(const client of [a,b]){
  const session=await expect(client,'/api/session',200);ensure(session.memberArea&&session.emailVerified,'verified member session');
  const me=await expect(client,'/api/me',200);ensure(me.dateOfBirth&&(!me.phoneRequired||me.phoneVerified),'adult profile and required phone');
  const p=await expect(client,'/api/me/profile',200);ensure(p.publicId&&p.displayName&&p.homeCity&&p.modes.some(m=>m.id==='friends'&&m.enabled&&m.visible),'visible Friends profile');profiles.push(p);
 }
 ensure(profiles[0].publicId!==profiles[1].publicId,'two distinct members');record('verified-session-and-profile');
 const [pa,pb]=profiles;
 for(const [client,other] of [[a,pb],[b,pa]]){const result=await expect(client,'/api/explore?mode=friends',200);ensure(result.people.some(p=>p.id===other.publicId),'mutual compatible discovery');}record('discover');
 const request=await expect(a,'/api/requests',201,'POST',{to:pb.publicId,mode:'friends'});record('request');
 await expect(b,'/api/requests/'+request.id+'/accept',200,'POST');record('accept');
 const conversation=await expect(a,'/api/conversations',201,'POST',{to:pb.publicId});
 const text='UAT smoke fixture '+randomUUID();
 const message=await expect(a,'/api/conversations/'+conversation.id+'/messages',201,'POST',{text});
 const received=await expect(b,'/api/conversations/'+conversation.id+'/messages',200);ensure(received.items.some(m=>m.id===message.id&&m.text===text),'message received by peer');record('message');
 await expect(b,'/api/reports',201,'POST',{subjectType:'message',subjectId:message.id,category:'other',text:'UAT smoke exercise only; fictional test message, not an allegation.',block:true});record('report-and-block');
 await expect(a,'/api/profiles/'+pb.publicId+'?mode=friends',404);
 await expect(a,'/api/conversations/'+conversation.id+'/messages',404,'POST',{text:'UAT blocked-send fixture'});record('blocked-access-refused');
 return {steps:7};
}
module.exports={journeyCore,ensure};
