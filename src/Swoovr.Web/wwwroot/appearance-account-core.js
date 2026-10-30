// Private appearance cache only. The owner is a session precondition, never authentication.
(function(root){
  'use strict';
  const guest=typeof module==='object'&&module.exports?require('./appearance-core.js'):root.SwoovrAppearance;
  const key='swoovr.appearance.account.v1',validOwner=value=>typeof value==='string'&&/^[a-f0-9]{64}$/.test(value);
  function envelope(storage){const text=storage.getItem(key);if(!text||text.length>32768)return null;try{const value=JSON.parse(text);return value?.version===1&&validOwner(value.owner)&&typeof value.json==='string'&&value.json.length<=16384?value:null;}catch{return null;}}
  function preference(json){try{const value=JSON.parse(json)?.current,normalized=guest.normalize(value);return value?.scheme===normalized.scheme&&value?.mode===normalized.mode?normalized:null;}catch{return null;}}
  function load(storage,owner){try{const value=envelope(storage);return{json:validOwner(owner)&&value?.owner===owner&&preference(value.json)?value.json:null,preference:validOwner(owner)&&value?.owner===owner?preference(value.json):null,available:true};}catch{return{json:null,preference:null,available:false};}}
  function same(a,b){return a?.scheme===b?.scheme&&a?.mode===b?.mode;}
  function revision(value){return typeof value?.baselineRevision==='string'&&/^\d{1,19}$/.test(value.baselineRevision)?BigInt(value.baselineRevision):null;}
  function dirty(value){return !!value.pending||!!value.conflict||value.explicitSaveRequired===true||!value.baseline||!same(value.current,value.baseline.preference);}
  function write(storage,owner,json,userEdit=false){
    if(!validOwner(owner)||typeof json!=='string'||json.length>16384||!preference(json))return 'unavailable';
    try{
      const existing=envelope(storage);if(existing&&existing.owner!==owner)return 'superseded';
      if(existing){
        const before=JSON.parse(existing.json),after=JSON.parse(json),old=revision(before),next=revision(after);
        if(!userEdit){
          if(old!==null&&next!==null&&next<old)return 'superseded';
          if(dirty(before)&&!same(before.current,after.current))return 'superseded';
          if(before.pending?.changeId&&after.pending?.changeId!==before.pending.changeId&&after.completedChangeId!==before.pending.changeId)return 'superseded';
          if(before.conflict&&!after.conflict)return 'superseded';
          if(!dirty(before)&&!dirty(after)&&same(before.current,after.current)&&old!==null&&old===next)return 'saved';
        }
      }
      storage.setItem(key,JSON.stringify({version:1,owner,json}));return 'saved';
    }catch{return 'unavailable';}
  }
  function save(storage,owner,json,userEdit=false){return write(storage,owner,json,userEdit)==='saved';}
  function clear(storage,owner){try{if(owner!==undefined&&envelope(storage)?.owner!==owner)return false;storage.removeItem(key);return true;}catch{return false;}}
  function owner(storage){try{return envelope(storage)?.owner??null;}catch{return null;}}
  const api={key,validOwner,load,save,write,clear,owner};if(typeof module==='object'&&module.exports)module.exports=api;else root.SwoovrAccountCache=api;
})(globalThis);
