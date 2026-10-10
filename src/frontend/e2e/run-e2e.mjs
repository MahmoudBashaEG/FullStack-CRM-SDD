import { spawnSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { mkdtempSync, readFileSync, existsSync, rmSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, join, sep } from 'node:path';
import { createServer } from 'node:net';
const root=resolve(fileURLToPath(new URL('../../../',import.meta.url)));
const frontend=join(root,'src/frontend');const env={...process.env};
if(!env.CRM_E2E_SQL_CONNECTION)throw new Error('CRM_E2E_SQL_CONNECTION is required.');
for(const key of Object.keys(env))if(key.startsWith('Authentication__Test'))delete env[key];
env.ConnectionStrings__Crm=env.CRM_E2E_SQL_CONNECTION;
env.DOTNET_ENVIRONMENT=env.ASPNETCORE_ENVIRONMENT='Testing';env.ASPNETCORE_URLS='https://localhost:7043';env.Auth__AllowedOrigins__0='https://localhost:4200';
env.NODE_EXTRA_CA_CERTS=join(root,'.local/dev-certs/localhost.pem');delete env.NODE_USE_SYSTEM_CA;env.NG_CLI_ANALYTICS='false';
env.Kestrel__Certificates__Default__Path=join(root,'.local/dev-certs/localhost.pem');env.Kestrel__Certificates__Default__KeyPath=join(root,'.local/dev-certs/localhost.key');
if(!existsSync(env.NODE_EXTRA_CA_CERTS)||!existsSync(join(root,'.local/dev-certs/localhost.key')))throw new Error('Existing trusted HTTPS PEM/key setup is required.');
const owner=randomUUID().replaceAll('-','');const directory=mkdtempSync(join(root,'.local/e2e-'));
if(!resolve(directory).startsWith(resolve(root,'.local')+sep))throw new Error('Unsafe owned directory.');
const fixture=join(root,'tests/scripts/fixtures/DevFixture/bin/Debug/net10.0/DevFixture.dll');
const run=(command,args,cwd=root,options={})=>{const result=spawnSync(command,args,{cwd,env,stdio:'inherit',shell:false,...options});if(result.error||result.status!==0)throw new Error('Owned test child failed.');return result;};
const portCheck=port=>new Promise((accept,reject)=>{const server=createServer();server.once('error',()=>reject(new Error('Required test port is occupied.')));server.listen(port,'localhost',()=>server.close(accept));});
let prepared=false;let exitCode=0;const manifest=join(directory,'accounts.json');
try{
  await Promise.all([portCheck(7043),portCheck(4200)]);
  const username=spawnSync('whoami',[],{encoding:'utf8'}).stdout.trim();run('icacls',[directory,'/inheritance:r','/grant:r',`${username}:(OI)(CI)F`]);
  run('pwsh',['-NoProfile','-File','scripts/e2e-database.ps1','-Mode','Prepare','-Owner',owner]);prepared=true;
  run('dotnet',['ef','database','update','--no-build','--project','src/backend/Crm.Infrastructure','--startup-project','src/backend/Crm.Api']);
  run('dotnet',[fixture,'prepare-local-auth',manifest,owner]);
  const setup=JSON.parse(readFileSync(manifest,'utf8'));
  const bootstrap=run('dotnet',[join(root,'src/backend/Crm.Api/bin/Debug/net10.0/Crm.Api.dll'),'bootstrap-initial-admin','--input-stdin'],root,{stdio:['pipe','pipe','pipe'],input:JSON.stringify({displayName:'Initial Admin',...setup.identities.admin})});
  if(JSON.parse(bootstrap.stdout.toString()).status!=='created')throw new Error('Real bootstrap did not create the initial Admin.');
  env.Jwt__SigningCertificateThumbprint=env.Jwt__SigningKeyId=setup.thumbprint;env.Jwt__CertificateStoreLocation='CurrentUser';env.Jwt__Issuer='urn:crm:local';env.Jwt__Audience='crm-api';
  env.CRM_AUTH_FIXTURE=manifest;env.DataProtection__KeyDirectory=join(directory,'data-protection');
  run('dotnet',[fixture,'ticket-auth',manifest]);
  const ticketSetup=JSON.parse(readFileSync(manifest,'utf8'));
  for(const [mode,first,second] of [['SeedTracking','trackingA','trackingB'],['SeedHistory','historyA','historyB']])
    run('pwsh',['-NoProfile','-File','scripts/e2e-database.ps1','-Mode',mode,'-Owner',owner,'-AgentOne',ticketSetup.fixtureRefs[first],'-AgentTwo',ticketSetup.fixtureRefs[second]]);
  const selection=process.argv.slice(2);
  // Full discovery is the default. Any scoped rerun must be explicitly selected.
  run(process.execPath,['node_modules/@playwright/test/cli.js','test',...selection],frontend);
}catch(error){console.error(error.message);exitCode=1;}
finally{
  try{if(existsSync(manifest))run('dotnet',[fixture,'cleanup-local-auth',manifest]);}catch(error){console.error(error.message);exitCode=1;}
  try{if(prepared)run('pwsh',['-NoProfile','-File','scripts/e2e-database.ps1','-Mode','Cleanup','-Owner',owner]);}catch(error){console.error(error.message);exitCode=1;}
  if(resolve(directory).startsWith(resolve(root,'.local')+sep))rmSync(directory,{recursive:true,force:true});
}
process.exitCode=exitCode;
