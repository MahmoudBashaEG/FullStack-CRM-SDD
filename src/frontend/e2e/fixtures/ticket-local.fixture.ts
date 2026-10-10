import {spawnSync} from 'node:child_process';
import {readFileSync,writeFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {randomUUID} from 'node:crypto';
import {request as apiRequest,expect,Page,BrowserContext} from '@playwright/test';
export const manifest=()=>JSON.parse(readFileSync(process.env.CRM_AUTH_FIXTURE!,'utf8'));
export function tokens():{staffA:string;staffB:string;denied:string;admin:string}{
  let setup=manifest();const expires=JSON.parse(Buffer.from(setup.ticketTokens.staffA.split('.')[1],'base64url').toString()).exp;
  if(expires*1000-Date.now()<90000){const result=spawnSync('dotnet',[resolve('../../tests/scripts/fixtures/DevFixture/bin/Debug/net10.0/DevFixture.dll'),'ticket-auth',process.env.CRM_AUTH_FIXTURE!],{cwd:resolve('../..'),env:process.env,stdio:'pipe'});if(result.status!==0)throw new Error('Real fixture signin renewal failed.');setup=manifest();}
  return setup.ticketTokens;
}
export const actorId=(role:'staffA'|'staffB'='staffA')=>manifest().ticketActors[role].userId;
export const fixtureReference=(name:string):string=>manifest().fixtureRefs[name];
export async function restoreTicketIdentity(page:Page,role:'staffA'|'staffB'|'denied'|'admin'='staffA'):Promise<void>{
  const setup=manifest();await page.context().addCookies(setup.ticketCookies[role]);await page.goto('/access');await expect(page.getByRole('button',{name:'Sign out',exact:true})).toBeVisible();
}
export async function preserveTicketCookies(context:BrowserContext,role:'staffA'|'staffB'|'denied'|'admin'='staffA'):Promise<void>{
  // A final navigation can still be restoring authentication. Capture the rotated
  // credential only after its response has settled, before closing this context.
  for(const page of context.pages())await page.waitForLoadState('networkidle');
  const setup=manifest();setup.ticketCookies[role]=(await context.storageState()).cookies.filter(cookie=>cookie.name==='__Host-crm-refresh');writeFileSync(process.env.CRM_AUTH_FIXTURE!,JSON.stringify(setup));
}
export async function closeTicketContext(context:BrowserContext,role:'staffA'|'staffB'|'denied'|'admin'):Promise<void>{try{await preserveTicketCookies(context,role);}finally{await context.close();}}
export async function createAgentReference(displayName='Synthetic Agent',isActive=true):Promise<string>{
  const client=await apiRequest.newContext({baseURL:'https://localhost:4200',ignoreHTTPSErrors:false,extraHTTPHeaders:{Authorization:`Bearer ${tokens().admin}`}});try{
    const response=await client.post('/api/agents',{data:{displayName,email:`${randomUUID()}@owned-fixture.test`,password:randomUUID()}});if(response.status()!==201)throw new Error('Actual Admin Agent creation failed.');const agent=await response.json();const setup=manifest();setup.agentRefs[agent.assignmentReferenceId]=agent.userId;writeFileSync(process.env.CRM_AUTH_FIXTURE!,JSON.stringify(setup));
    if(!isActive){const disabled=await client.put(`/api/agents/${agent.userId}/availability`,{data:{isActive:false,version:agent.version}});if(disabled.status()!==200)throw new Error('Actual Agent disablement failed.');}return agent.assignmentReferenceId;
  }finally{await client.dispose();}
}
export async function updateAgentReference(id:string,displayName:string,isActive:boolean):Promise<void>{
  const setup=manifest(),userId=setup.agentRefs[id];if(!userId)throw new Error('Only locally created fixture Agents can be administered.');const client=await apiRequest.newContext({baseURL:'https://localhost:4200',ignoreHTTPSErrors:false,extraHTTPHeaders:{Authorization:`Bearer ${tokens().admin}`}});try{
    const get=await client.get(`/api/agents/${userId}`);if(get.status()!==200)throw new Error('Actual Agent detail failed.');const agent=await get.json();const rename=await client.patch(`/api/agents/${userId}`,{data:{displayName,version:agent.version}});if(rename.status()!==200)throw new Error('Actual Agent rename failed.');const updated=await rename.json();const state=await client.put(`/api/agents/${userId}/availability`,{data:{isActive,version:updated.version}});if(state.status()!==200)throw new Error('Actual Agent availability failed.');
  }finally{await client.dispose();}
}
