// Opt-in local realm experiment only. Never changes or deletes an existing key provider.
import { random } from './security.mjs';
const prefix='swoovr-investigation-rotation-';
const providerType='org.keycloak.keys.KeyProvider';
function requireValue(value){if(!value)throw new Error('Local rotation evidence rejected');}
export async function withTemporarySigningKey(api,exercise){
  const realm=await api('');
  requireValue(realm.realm==='swoovr-dev' && typeof realm.id==='string' && realm.id.length>0);
  const existing=await api('/components');
  requireValue(Array.isArray(existing) && !existing.some(c=>c.name?.startsWith(prefix)));
  const beforeKid=(await api('/keys')).active?.RS256;
  requireValue(typeof beforeKid==='string' && beforeKid.length>0);
  const priorities=existing.filter(c=>c.providerType===providerType).map(c=>Number(c.config?.priority?.[0]??0));
  requireValue(priorities.every(p=>Number.isSafeInteger(p)&&p>=0&&p<10000));
  const name=prefix+random().slice(0,12);
  const definition={name,parentId:realm.id,providerType,providerId:'rsa-generated',
    config:{priority:[String(Math.max(100,...priorities)+100)],enabled:['true'],active:['true'],algorithm:['RS256'],keySize:['2048']}};
  let attempted=false;
  const owned=c=>c.name===name && c.parentId===realm.id && c.providerType===providerType && c.providerId==='rsa-generated';
  try{
    // Even a lost POST response may have created the component; finally discovers it by this unique name.
    attempted=true;
    await api('/components','POST',definition);
    const created=(await api('/components')).filter(c=>c.name===name);
    requireValue(created.length===1 && owned(created[0]));
    const newKid=(await api('/keys')).active?.RS256;
    requireValue(typeof newKid==='string' && newKid!==beforeKid);
    return await exercise({beforeKid,newKid});
  }finally{
    if(attempted){
      const candidates=(await api('/components')).filter(c=>c.name===name);
      requireValue(candidates.length<=1);
      for(const c of candidates){
        requireValue(owned(c) && typeof c.id==='string' && /^[a-zA-Z0-9-]+$/.test(c.id));
        await api('/components/'+c.id,'DELETE');
      }
      requireValue(!(await api('/components')).some(c=>c.name===name));
      requireValue((await api('/keys')).active?.RS256===beforeKid);
    }
  }
}
