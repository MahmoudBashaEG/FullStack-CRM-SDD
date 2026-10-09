import https from 'node:https';
import {readFileSync} from 'node:fs';
import assert from 'node:assert/strict';
const setup=JSON.parse(readFileSync(process.env.CRM_AUTH_FIXTURE,'utf8'));
const ca=readFileSync(process.env.NODE_EXTRA_CA_CERTS);const jar=new Map();let csrf;
const send=(path,body=null,token=null,origin='https://localhost:4200')=>new Promise((accept,reject)=>{
  const content=body===null?null:JSON.stringify(body);
  const request=https.request(new URL(path,'https://localhost:4200'),{ca,rejectUnauthorized:true,method:body===null?'GET':'POST',headers:{Origin:origin,...(csrf?{'X-CSRF-TOKEN':csrf}:{}),...(jar.size?{Cookie:[...jar].map(([key,value])=>`${key}=${value}`).join('; ')}:{}),...(token?{Authorization:`Bearer ${token}`} : {}),...(content?{'Content-Type':'application/json','Content-Length':Buffer.byteLength(content)}:{})}},response=>{
    let text='';response.on('data',chunk=>text+=chunk);response.on('end',()=>{
      for(const cookie of response.headers['set-cookie']??[]){const first=cookie.split(';')[0];const split=first.indexOf('=');const key=first.slice(0,split);const value=first.slice(split+1);if(value)jar.set(key,value);else jar.delete(key);}
      accept({status:response.statusCode,headers:response.headers,text});
    });
  });request.on('error',()=>reject(new Error('Strict local proxy verification failed.')));if(content)request.write(content);request.end();
});
const anti=await send('/api/auth/csrf');assert.equal(anti.status,200);csrf=JSON.parse(anti.text).requestToken;
const login=await send('/api/auth/signin',setup.identities.admin);assert.equal(login.status,200);const envelope=JSON.parse(login.text);
const cookie=login.headers['set-cookie'].find(value=>value.startsWith('__Host-crm-refresh='));assert.ok(cookie&&/secure/i.test(cookie)&&/httponly/i.test(cookie)&&/samesite=strict/i.test(cookie)&&/path=\//i.test(cookie)&&!/domain=/i.test(cookie));
assert.equal((await send('/api/auth/me',null,envelope.accessToken)).status,200);
assert.equal((await send('/api/auth/refresh',{},null,'https://outside.test')).status,403);
assert.equal((await send('/api/auth/refresh',{})).status,200);
assert.equal((await send('/api/auth/signout',{},envelope.accessToken)).status,204);
assert.equal((await send('/api/auth/me',null,envelope.accessToken)).status,401);
const docs=await send('/api-docs');assert.equal(docs.status,200);assert.ok(docs.text.includes('<html'));
console.log('PASS strict HTTPS /api proxy: Origin, Bearer, Secure HttpOnly cookies, rotation and signout.');
