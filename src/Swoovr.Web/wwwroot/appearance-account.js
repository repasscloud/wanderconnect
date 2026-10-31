// Browser binding; cache JSON remains text to preserve exact .NET64-bit revisions.
(function(){
 'use strict';const cache=window.SwoovrAccountCache;let active=null,callback=null,started=false;
 const state=()=>window.SwoovrGuestAppearance;
 function guest(){const s=state();if(!s)return;const value=window.SwoovrAppearance.load(s.storage);s.guestPreference=value.preference;s.preference=value.preference;s.apply();}
 function start(owner){
  const s=state();if(!s)return;
  owner=cache.validOwner(owner)?owner:null;
  if(started&&owner===active&&s.activeAccount===owner)return;
  started=true;
  const previous=active;active=owner;s.activeAccount=owner;
  const stored=cache.owner(s.storage);if(!owner)cache.clear(s.storage);else if(stored&&stored!==owner)cache.clear(s.storage,stored);
  const value=owner?cache.load(s.storage,owner):null;s.preference=value?.preference??window.SwoovrAppearance.load(s.storage).preference;s.apply();
  if(previous&&previous!==owner)notify('owner-changed');
 }
 function notify(reason){if(callback)callback.reference.invokeMethodAsync('AppearanceBrowserChanged',reason).catch(()=>{});}
 window.SwoovrAccountAppearance={
  start,
  load(owner){if(active!==owner)return null;return cache.load(state()?.storage,owner).json;},
  save(owner,json,userEdit){return active===owner?cache.write(state()?.storage,owner,json,userEdit):"superseded";},
  display(owner,preference){if(active!==owner)return false;state().preference=window.SwoovrAppearance.normalize(preference);state().apply();return true;},
  guestPreference(){return window.SwoovrAppearance.load(state()?.storage).preference;},
  connect(owner,reference,id){start(owner);callback={reference,id};},
  disconnect(id){if(callback?.id===id)callback=null;},
  clear(owner){cache.clear(state()?.storage,owner);if(active===owner){active=null;state().activeAccount=null;guest();}},
 };
 window.addEventListener('pageshow',event=>{if(event.persisted&&active&&cache.owner(state()?.storage)!==active){active=null;state().activeAccount=null;guest();notify('owner-changed');}});
 window.addEventListener('online',()=>notify('online'));
 window.addEventListener('focus',()=>notify('focus'));
 window.addEventListener('storage',event=>{
  if(event.key!==cache.key&&event.key!==null)return;
  const owner=cache.owner(state()?.storage);
  if(active&&owner!==active){const old=active;active=null;state().activeAccount=null;guest();notify('owner-changed');}
  else if(active){const latest=cache.load(state()?.storage,active);if(latest.preference){state().preference=latest.preference;state().apply();}notify('cache-updated');}
 });
 document.addEventListener('submit',event=>{const form=event.target;if(form instanceof HTMLFormElement&&new URL(form.action,location.href).pathname==='/account/sign-out'){cache.clear(state()?.storage);active=null;if(state())state().activeAccount=null;guest();notify('owner-changed');}},true);
})();
