// Mirrors paints.py; regression tests compare stops at multiple angles.
function flagEdges(values,angle,weights,balance){
 weights=weights||values.map(()=>1);if(weights.length!==values.length||weights.some(w=>w<=0))throw Error('Each flag colour needs a positive weight.');
 let sum=weights.reduce((a,b)=>a+b,0),acc=0;
 const q=weights.slice(0,-1).map(w=>(acc+=w)/sum);
 if(balance==='linear')return q;
 const r=angle*Math.PI/180,dx=Math.cos(r),dy=Math.sin(r);
 const p=DATA.samples.map(([x,y])=>.5+(x-.5)*dx+(y-.5)*dy).sort((a,b)=>a-b);
 // Python round uses ties-to-even; implement it for parity at the median.
 const roundEven=x=>{const f=Math.floor(x);return x-f===.5?(f%2?f+1:f):Math.round(x)};
 return q.map(c=>p[roundEven(c*(p.length-1))]);
}
function flagStops(values,angle,style,weights,balance){
 if(style==='blend')return values.map((c,i)=>[i/(values.length-1),c]);
 const edges=[0,...flagEdges(values,angle,weights,balance),1],out=[];
 values.forEach((c,i)=>{const left=style==='soft'?Math.min(i?edges[i]-edges[i-1]:1,edges[i+1]-edges[i])*.12:0;
 const right=style==='soft'?Math.min(edges[i+1]-edges[i],i+2<edges.length?edges[i+2]-edges[i+1]:1)*.12:0;
 out.push([edges[i]+(i?left:0),c],[edges[i+1]-(i<values.length-1?right:0),c]);});return out;
}
function flagPaint(id,a,angle,style='blend',weights=null,balance='area'){
 if(a.length===1)return {def:'',paint:a[0]};
 if(style==='union-jack'){
  if(a.length!==3)throw Error('Pink Union Jack needs exactly three colours: pink, white, field.');
  const [pink,white,field]=a;let content=`<rect x="-600" y="-600" width="1800" height="1600" fill="${field}"/>`;
  for(const [width,colour] of [[90,white],[34,pink]])content+=`<path d="M-600 -400L1200 800M-600 800L1200 -400" fill="none" stroke="${colour}" stroke-width="${width}"/>`;
  for(const [width,colour] of [[130,white],[76,pink]])content+=`<path d="M300 -800V1200M-900 200H1500" fill="none" stroke="${colour}" stroke-width="${width}"/>`;
  return {def:`<pattern id="${id}" width="1" height="1" viewBox="0 0 600 400" preserveAspectRatio="none"><g transform="rotate(${angle} 300 200)">${content}</g></pattern>`,paint:`url(#${id})`};
 }
 const r=angle*Math.PI/180,dx=50*Math.cos(r),dy=50*Math.sin(r);
 return {def:`<linearGradient id="${id}" x1="${50-dx}%" y1="${50-dy}%" x2="${50+dx}%" y2="${50+dy}%">${flagStops(a,angle,style,weights,balance).map(([p,c])=>`<stop offset="${p.toFixed(6)}" stop-color="${c}"/>`).join('')}</linearGradient>`,paint:`url(#${id})`};
}
