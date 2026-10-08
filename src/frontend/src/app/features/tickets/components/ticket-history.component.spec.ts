import { TestBed } from '@angular/core/testing';
import { TicketHistoryComponent } from './ticket-history.component';
import { TicketHistory } from '../models/ticket.models';

const actor={ issuer:'https://example.test',subject:'history-staff',displayName:'Historical Staff <img>' };
const common={ revision:'9007199254740993',sequence:1,actor,occurredAtUtc:'2026-10-08T09:00:00Z' };
const refA={id:'category-a',displayName:'Historical category A'}; const refB={id:'category-b',displayName:'Historical category B'};
const agentA={id:'agent-a',displayName:'Historical agent A'}; const agentB={id:'agent-b',displayName:'Historical agent B'};
const entries:TicketHistory[]=[
 { ...common,id:'created',revision:'1',kind:'Created',field:null,previousValue:null,newValue:{reference:'TKT-HISTORY',subject:'Original <script>',description:'وصف أصلي',category:refA,priority:'Normal',status:'Open',assignedAgent:null}},
 { ...common,id:'subject',kind:'DetailsChanged',field:'subject',previousValue:'Old <script>',newValue:'New <img src=x>'},
 { ...common,id:'description',sequence:2,kind:'DetailsChanged',field:'description',previousValue:'وصف قديم',newValue:'وصف جديد\n محفوظ'},
 { ...common,id:'category',sequence:3,kind:'DetailsChanged',field:'category',previousValue:refA,newValue:refB},
 { ...common,id:'priority',sequence:4,kind:'DetailsChanged',field:'priority',previousValue:'Low',newValue:'Urgent'},
 { ...common,id:'assigned',kind:'AssignmentChanged',field:'assignedAgent',previousValue:null,newValue:agentA},
 { ...common,id:'reassigned',kind:'AssignmentChanged',field:'assignedAgent',previousValue:agentA,newValue:agentB},
 { ...common,id:'cleared',kind:'AssignmentChanged',field:'assignedAgent',previousValue:agentB,newValue:null},
 { ...common,id:'status',kind:'StatusChanged',field:'status',previousValue:'InProgress',newValue:'Resolved'},
 { ...common,id:'closed',kind:'StatusChanged',field:'status',previousValue:'Resolved',newValue:'Closed'},
 { ...common,id:'reopened',kind:'StatusChanged',field:'status',previousValue:'Closed',newValue:'Open'}
];
describe('TicketHistoryComponent',()=>{
 async function ready(items:TicketHistory[]=entries,page=1,hasMore=false) { TestBed.configureTestingModule({imports:[TicketHistoryComponent]}); const fixture=TestBed.createComponent(TicketHistoryComponent); fixture.componentRef.setInput('page',{items,page,pageSize:25,hasMore}); fixture.detectChanges(); await fixture.whenStable(); return fixture; }
 it('renders every kind/field/null snapshot, actor/time and opaque large revision safely in supplied order',async()=>{
  const fixture=await ready(); const rows=Array.from(fixture.nativeElement.querySelectorAll('li')) as HTMLElement[]; expect(rows).toHaveLength(11);
  const expected=[['Original <script>','وصف أصلي','Historical category A','Normal','Open','Unassigned'],['Old <script>','New <img src=x>'],['وصف قديم','وصف جديد\n محفوظ'],['Historical category A','Historical category B'],['Low','Urgent'],['Unassigned','Historical agent A'],['Historical agent A','Historical agent B'],['Historical agent B','Unassigned'],['In Progress','Resolved'],['Resolved','Closed'],['Closed','Open']];
  for(const [index,row] of rows.entries()) { for(const text of expected[index]) expect(row.textContent).toContain(text); expect(row.textContent).toContain('Historical Staff <img>'); expect(row.querySelector('time')!.getAttribute('datetime')).toBe(common.occurredAtUtc); }
  expect(rows[1].textContent).toContain('9007199254740993'); expect(fixture.nativeElement.querySelector('script,img')).toBeNull();
  expect(Array.from(fixture.nativeElement.querySelectorAll('button'),(button:any)=>button.textContent.trim())).toEqual(['Previous history','Next history']);
 });
 it('pages via read-only events and guards page bounds and loading',async()=>{
  const fixture=await ready(entries,2,true); const requested=vi.fn(); fixture.componentInstance.requested.subscribe(requested); const buttons=fixture.nativeElement.querySelectorAll('nav button'); buttons[0].click(); buttons[1].click(); expect(requested.mock.calls).toEqual([[1],[3]]);
  fixture.componentRef.setInput('loading',true); fixture.detectChanges(); buttons[0].click(); buttons[1].click(); expect(requested).toHaveBeenCalledTimes(2); expect(fixture.nativeElement.textContent).toContain('Loading history');
  fixture.componentRef.setInput('loading',false); fixture.componentRef.setInput('page',{items:entries,page:1,pageSize:25,hasMore:false}); fixture.detectChanges(); expect(buttons[0].disabled).toBe(true); expect(buttons[1].disabled).toBe(true);
 });
 it('distinguishes empty success from a failure and retries the attempted page',async()=>{
  const fixture=await ready([],1,false); expect(fixture.nativeElement.textContent).toContain('No history entries.');
  fixture.componentRef.setInput('page',{items:entries,page:1,pageSize:25,hasMore:true}); fixture.componentRef.setInput('error','History failed'); fixture.componentRef.setInput('retryPage',2); fixture.detectChanges(); const requested=vi.fn(); fixture.componentInstance.requested.subscribe(requested); fixture.nativeElement.querySelector('button').click(); expect(requested).toHaveBeenCalledWith(2); expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('History failed');
 });
});
