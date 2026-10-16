// Temporary optional MFA on one owned fictional fixture. Never resets other credentials.
import {random} from './security.mjs';
function requireValue(value){if(!value)throw Error('Local MFA evidence rejected');}
export async function withTemporaryOtp(api,fixture,exercise){
 requireValue(fixture?.owner==='datingapp-local-investigation-v1'&&fixture.fictionalAdult===true&&/^[a-zA-Z0-9-]+$/.test(fixture.userId??''));
 const path='/users/'+fixture.userId;
 const user=await api(path);
 requireValue(user.id===fixture.userId&&typeof user.username==='string'&&typeof fixture.username==='string'&&user.username.toLowerCase()===fixture.username.toLowerCase());
 const actions=user.requiredActions??[];
 const before=await api(path+'/credentials');
 requireValue(Array.isArray(actions)&&actions.length===0&&Array.isArray(before)&&!before.some(c=>c.type==='otp'));
 const beforeIds=new Set(before.map(c=>c.id));
 const label='datingapp-investigation-mfa-'+random().slice(0,12);
 try{
  await api(path,'PUT',{requiredActions:['CONFIGURE_TOTP']});
  return await exercise({label});
 }finally{
  // Discover even after a lost enrollment response; remove only this unique test label.
  let foreignOtp=false;
  for(const credential of await api(path+'/credentials')){
   if(credential.type!=='otp'||beforeIds.has(credential.id))continue;
   if(credential.userLabel!==label){foreignOtp=true;continue;}
   requireValue(typeof credential.id==='string'&&/^[a-zA-Z0-9-]+$/.test(credential.id));
   await api(path+'/credentials/'+credential.id,'DELETE');
  }
  const current=await api(path);
  const remaining=(current.requiredActions??[]).filter(action=>action!=='CONFIGURE_TOTP');
  await api(path,'PUT',{requiredActions:remaining});
  const after=await api(path+'/credentials');
  requireValue(!foreignOtp&&before.every(old=>after.some(c=>c.id===old.id))&&!after.some(c=>c.type==='otp'&&c.userLabel===label));
  requireValue(!(await api(path)).requiredActions?.includes('CONFIGURE_TOTP'));
 }
}
