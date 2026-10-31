// Device-only actions. Never transmit coordinates, destination or SMS body to a Swoovr endpoint.
window.swoovrMeeting=Object.freeze({
  clock(){const local=d=>{const pad=n=>String(n).padStart(2,'0');return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;};return {zone:Intl.DateTimeFormat().resolvedOptions().timeZone||'UTC',start:local(new Date(Date.now()+3600000)),end:local(new Date(Date.now()+10800000))};},
  async compose(number,text,includeLocation){
    if(!/^\+[1-9][0-9]{7,14}$/.test(number))throw new Error('SMS destination unavailable.');
    if(includeLocation){const position=await new Promise((resolve,reject)=>navigator.geolocation.getCurrentPosition(resolve,reject,{enableHighAccuracy:false,timeout:10000,maximumAge:0}));text+='\nOptional device-provided location (not live tracking): https://maps.google.com/?q='+encodeURIComponent(position.coords.latitude+','+position.coords.longitude);}
    window.location.href='sms:'+number+'?body='+encodeURIComponent(text);
  }
});
