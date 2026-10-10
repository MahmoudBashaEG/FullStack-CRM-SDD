import { restoreTicketIdentity,closeTicketContext } from '../fixtures/ticket-local.fixture';
import { actorId,fixtureReference,createAgentReference } from '../fixtures/ticket-local.fixture';
import { test,expect,tokens,navigateTicket } from '../fixtures/auth';
import { provisionReferences } from '../fixtures/provision-references';

const reference='TKT-60000000000000000000000000000001';
const headers=()=>({Authorization:`Bearer ${tokens().staffA}`});
const expectedRevisions=['1','2','2','2','2','9','10','11','12','13','14','15','16',...Array.from({length:18},(_,index)=>String(index+100)),'9007199254740992','9007199254740993'];
test.beforeEach(async({request})=>{
 const readiness='/api/ticket-reference-data/categories'; expect((await request.get(readiness)).status()).toBe(401); const ready=await request.get(readiness,{headers:headers()}); expect(ready.status()).toBe(200); expect(await ready.json()).toContainEqual({id:'00000000-0000-0000-0000-000000000001',displayName:'Uncategorised'});
});

test('reviews all history kinds in numeric order across pages with tied times and unchanged snapshots after renaming',async({page,request})=>{
 const response=await request.get(`/api/tickets/${reference}/history?pageSize=100`,{headers:headers()}); expect(response.status()).toBe(200); const original=await response.json(); expect(original.items).toHaveLength(33);
 expect(original.items.map((entry:any)=>entry.revision)).toEqual(expectedRevisions); expect(original.items.map((entry:any)=>entry.id)).toEqual(Array.from({length:33},(_,index)=>`70000000-0000-0000-0000-${String(index+1).padStart(12,'0')}`));
 for(const entry of original.items) { expect(entry.occurredAtUtc).toBe('2026-10-08T09:00:00+00:00'); expect(entry.actor.issuer).toBe('https://example.test'); expect(entry.actor.displayName).toMatch(/^Historical /); }
 await navigateTicket(page,`/tickets/${reference}`); const rows=page.locator('app-ticket-history li'); await expect(rows).toHaveCount(25);
 const expected=[['History original subject','وصف قديم','History category one','Normal','Open','Unassigned'],['History original subject','New <img>'],['وصف قديم','تفاصيل جديدة'],['History category one','History category two'],['Normal','High'],['Unassigned','History agent one'],['History agent one','History agent two'],['History agent two','Unassigned'],['Open','In Progress'],['In Progress','Resolved'],['Resolved','Closed'],['Closed','Open'],['Unassigned','History agent one']];
 for(const [index,texts] of expected.entries()) { const row=rows.nth(index); for(const text of texts) await expect(row).toContainText(text); await expect(row).toContainText(original.items[index].actor.displayName); await expect(row.locator('time')).toHaveAttribute('datetime',original.items[index].occurredAtUtc); }
 await expect(page.locator('app-ticket-history img,app-ticket-history script')).toHaveCount(0); await expect(page.getByRole('button',{name:/^(Edit|Delete).*history/i})).toHaveCount(0);
 await page.getByRole('button',{name:'Next history',exact:true}).click(); await expect(rows).toHaveCount(8); await expect(page.locator('app-ticket-history nav')).toContainText('Page 2'); await expect(rows.nth(6)).toContainText('9007199254740992'); await expect(rows.nth(7)).toContainText('9007199254740993'); await expect(page.getByRole('button',{name:'Next history',exact:true})).toBeDisabled();
 const detail=await (await request.get(`/api/tickets/${reference}`,{headers:headers()})).json();
 await provisionReferences({schemaVersion:1,categories:[{id:'40000000-0000-0000-0000-000000000002',displayName:'Renamed current history category',isAvailable:false}],agents:[{id:fixtureReference('historyA'),issuer:'https://example.test',subject:'history-agent-one',displayName:'Renamed current history agent',isEligible:false}]});
 const renamed=await (await request.get(`/api/tickets/${reference}`,{headers:headers()})).json(); expect(renamed.editVersion).toBe(detail.editVersion); expect(renamed.updatedAtUtc).toBe(detail.updatedAtUtc); expect(renamed.category.displayName).toBe('Renamed current history category'); expect(renamed.assignedAgent.displayName).toBe('Renamed current history agent');
 expect(await (await request.get(`/api/tickets/${reference}/history?pageSize=100`,{headers:headers()})).json()).toEqual(original);
 await page.reload(); await expect(rows).toHaveCount(25); await expect(page.locator('main dl').first()).toContainText('Renamed current history category'); await expect(page.locator('main dl').first()).toContainText('Renamed current history agent'); await expect(rows.nth(3)).toContainText('History category two'); await expect(rows.nth(5)).toContainText('History agent one'); await expect(rows.nth(3)).not.toContainText('Renamed');
});

test('history failures retry the requested page and stay distinct from empty results',async({page,request})=>{
 await navigateTicket(page,`/tickets/${reference}`); await expect(page.locator('app-ticket-history li')).toHaveCount(25);
 await page.route(`**/api/tickets/${reference}/history?page=2&pageSize=25`,route=>route.fulfill({status:503,contentType:'application/problem+json',body:JSON.stringify({code:'service_unavailable'})}),{times:1});
 await page.getByRole('button',{name:'Next history',exact:true}).click(); await expect(page.locator('app-ticket-history [role="alert"]')).toContainText('History could not be loaded'); await expect(page.locator('app-ticket-history li')).toHaveCount(25);
 const retry=page.waitForResponse(response=>response.url().includes(`/api/tickets/${reference}/history?page=2&pageSize=25`)); await page.getByRole('button',{name:'Retry history',exact:true}).click(); expect((await retry).status()).toBe(200); await expect(page.locator('app-ticket-history li')).toHaveCount(8); await expect(page.locator('app-ticket-history nav')).toContainText('Page 2');
 const empty=await request.get(`/api/tickets/${reference}/history?page=3`,{headers:headers()}); expect(empty.status()).toBe(200); expect(await empty.json()).toEqual({items:[],page:3,pageSize:25,hasMore:false});
 expect((await request.get(`/api/tickets/${reference}/history`)).status()).toBe(401); expect((await request.get(`/api/tickets/${reference}/history`,{headers:{Authorization:`Bearer ${tokens().denied}`}})).status()).toBe(403);
});
