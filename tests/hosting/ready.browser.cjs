// Owned local hosts: readiness is database/schema/fence and portal-to-API reachability, never external delivery.
const assert=require('node:assert/strict');if(!process.env.SWOOVR_TEST_APP_DATABASE)throw Error('Use owned runner.');
const {api,web}=require('../auth/local-tokens.cjs');
(async()=>{let checks=0;for(const [name,base] of [['api',api],['web',web],['admin','http://localhost:5082']]){
 const live=await fetch(base+'/health/live');assert.equal(live.status,200);checks++;
 const ready=await fetch(base+'/health/ready');assert.equal(ready.status,200,name);assert.deepEqual(await ready.json(),{status:'ready'});assert.match(ready.headers.get('cache-control'),/no-store/);checks++;
 const csp=ready.headers.get('content-security-policy');assert.ok(csp.includes("frame-ancestors 'none'"));assert.ok(!csp.includes("'unsafe-eval'"));if(name==='web')assert.ok(csp.includes("'wasm-unsafe-eval'"));checks++;
 assert.equal(ready.headers.get('x-content-type-options'),'nosniff');checks++;
}console.log(`PASS local host readiness/security headers: ${checks} checks; database/schema/fence and portal HTTP only, no hosted/provider claim.`);})().catch(e=>{console.error(e);process.exitCode=1});
