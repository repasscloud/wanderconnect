// Local test authenticator only, not an application MFA implementation.
import {createHmac} from 'node:crypto';
export function totp(secret,seconds=Math.floor(Date.now()/1000),digits=6){
 if(typeof secret!=='string'||!(/^[A-Z2-7]{16,128}$/.test(secret))||!Number.isSafeInteger(seconds)||seconds<0||![6,8].includes(digits))throw Error('Invalid local OTP input');
 const alphabet='ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';let bits=0,value=0;const bytes=[];
 for(const char of secret){value=(value<<5)|alphabet.indexOf(char);bits+=5;if(bits>=8){bits-=8;bytes.push((value>>>bits)&255);value&=(1<<bits)-1;}}
 const counter=Buffer.alloc(8);counter.writeBigUInt64BE(BigInt(Math.floor(seconds/30)));
 const digest=createHmac('sha1',Buffer.from(bytes)).update(counter).digest();
 const offset=digest.at(-1)&15;
 return String((digest.readUInt32BE(offset)&0x7fffffff)%(10**digits)).padStart(digits,'0');
}
