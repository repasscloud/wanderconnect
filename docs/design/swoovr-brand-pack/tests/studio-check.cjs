// Execute generated studio code against a small DOM stub, without a browser.
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
const html=fs.readFileSync(path.join(__dirname,'../PREVIEW.html'),'utf8'),script=html.match(/<script>([\s\S]*?)<\/script>/)[1];
const elements={};function element(){return {value:'',textContent:'',innerHTML:'',options:[],add(o){this.options.push(o);if(!this.value)this.value=o.value},append(){},addEventListener(){},click(){}}}
const document={getElementById(id){return elements[id]??=element()},createElement(){return element()},createTextNode(text){return {textContent:text}}};
const context=vm.createContext({document,Option:function(label,value){return {label,value}},console,Blob,URL,setTimeout});vm.runInContext(script,context);
vm.runInContext(`
let checked=0;
for(const name of Object.keys(DATA.themes)){
 $('preset').value=name;preset();
 for(const angle of [0,90,138])for(const layout of ['bird','horizontal','app-rounded','reddit-banner','reddit-mobile-banner']){
  $('angle').value=angle;$('layout').value=layout;update();
  if($('error').textContent||!current.includes('<svg'))throw Error(name+': '+$('error').textContent);
  checked++;
 }
}
$('preset').value='pride-trans-5';preset();$('swapPaints').onclick();
if($('backgroundStyle').value!=='soft'||$('style').value!=='blend')throw Error('Swap lost flag treatment');
$('bird').value='#0D0F12';$('backgroundAngle').value='138';update();
if($('error').textContent||!current.includes('#0D0F12')||!current.includes('studio-background'))throw Error('Solid bird on flag background failed');
$('swapPaints').onclick();if($('style').value!=='soft')throw Error('Swap back lost flag');
$('bird').value='invalid';update();
if(current!==''||!$('error').textContent)throw Error('Invalid input was not rejected');
console.log('PASS: '+checked+' studio preset/angle/layout combinations; invalid colour rejected.');
`,context);
assert.equal(elements.preset.options.length,18);
