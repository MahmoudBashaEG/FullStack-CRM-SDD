import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, Subject } from 'rxjs';
import { TicketDetailPage } from './ticket-detail.page';
import { sampleTicket } from '../testing/ticket-fixtures';

describe('TicketDetailPage', () => {
  let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ imports: [TicketDetailPage], providers: [provideHttpClient(), provideHttpClientTesting(), { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ reference: 'TKT-TEST' })) } }] }); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  for (const outcome of ['success', 'failure'] as const) {
    it(`cancels a previous-ticket retry before its late ${outcome} can alter the current ticket`, async () => {
      const params = new Subject<ReturnType<typeof convertToParamMap>>();
      Object.assign(TestBed.inject(ActivatedRoute), { paramMap: params.asObservable() });
      const fixture = TestBed.createComponent(TicketDetailPage); fixture.detectChanges();
      params.next(convertToParamMap({ reference: 'TKT-A' }));
      http.expectOne('/api/tickets/TKT-A').flush({}, { status: 503, statusText: 'Unavailable' });
      fixture.componentInstance.reload(); const obsolete = http.expectOne('/api/tickets/TKT-A');
      params.next(convertToParamMap({ reference: 'TKT-B' }));
      const current = { ...sampleTicket, reference: 'TKT-B', subject: 'Ticket B', editVersion: 'AAAAAAAAAAI=' };
      http.expectOne('/api/tickets/TKT-B').flush(current);
      http.expectOne('/api/tickets/TKT-B/history?page=1&pageSize=25').flush({ items: [], page: 1, pageSize: 25, hasMore: false });
      http.expectOne('/api/ticket-reference-data/categories').flush([]); http.expectOne('/api/ticket-reference-data/agents').flush([]);
      await fixture.whenStable(); fixture.detectChanges();
      const input = fixture.nativeElement.querySelector('#edit-subject') as HTMLInputElement;
      input.value = 'Unsaved B draft'; input.dispatchEvent(new Event('input'));
      const cancelled = obsolete.cancelled;
      if (!cancelled) {
        if (outcome === 'success') obsolete.flush({ ...sampleTicket, reference: 'TKT-A' });
        else obsolete.flush({}, { status: 404, statusText: 'Not found' });
        // Consume only effects of the defective implementation so the regression fails on behavior.
        for (const request of http.match(request => request.url.endsWith('/history') || request.url.includes('/ticket-reference-data/')))
          request.flush(request.request.url.endsWith('/history') ? { items: [], page: 1, pageSize: 25, hasMore: false } : []);
      }
      await fixture.whenStable(); fixture.detectChanges();
      expect(cancelled).toBe(true); expect(fixture.componentInstance.ticket()).toEqual(current);
      expect(fixture.componentInstance.session.seed()).toEqual(current); expect(fixture.componentInstance.error()).toBe('');
      expect(fixture.componentInstance.history()).toEqual({ items: [], page: 1, pageSize: 25, hasMore: false });
      expect(input.value).toBe('Unsaved B draft'); expect(fixture.componentInstance.loading()).toBe(false);
      http.expectNone('/api/tickets/TKT-A/history?page=1&pageSize=25');
    });
  }
  it('keeps a normal same-ticket retry working and cancels an older retry when a newer one starts', async () => {
    const fixture = TestBed.createComponent(TicketDetailPage); fixture.detectChanges();
    http.expectOne('/api/tickets/TKT-TEST').flush({}, { status: 503, statusText: 'Unavailable' });
    fixture.componentInstance.reload(); const obsolete = http.expectOne('/api/tickets/TKT-TEST');
    fixture.componentInstance.reload(); const current = http.expectOne('/api/tickets/TKT-TEST');
    const cancelled = obsolete.cancelled;
    if (!cancelled) obsolete.flush({}, { status: 503, statusText: 'Unavailable' });
    current.flush(sampleTicket);
    http.expectOne('/api/tickets/TKT-TEST/history?page=1&pageSize=25').flush({ items: [], page: 1, pageSize: 25, hasMore: false });
    http.expectOne('/api/ticket-reference-data/categories').flush([]); http.expectOne('/api/ticket-reference-data/agents').flush([]);
    await fixture.whenStable(); fixture.detectChanges();
    expect(cancelled).toBe(true); expect(fixture.componentInstance.ticket()).toEqual(sampleTicket);
    expect(fixture.componentInstance.error()).toBe(''); expect(fixture.componentInstance.loading()).toBe(false);
    expect(fixture.nativeElement.querySelector('h1').textContent).toBe(sampleTicket.reference);
  });
  it('renders inactive references and user text safely and reads creation history', async () => {
    const fixture = TestBed.createComponent(TicketDetailPage); fixture.detectChanges();
    http.expectOne('/api/tickets/TKT-TEST').flush({ reference: 'TKT-TEST', subject: '<img src=x onerror=alert(1)>', description: 'وصف',
      category: { id: 'category', displayName: 'Inactive category', isAvailable: false }, priority: 'Normal', status: 'Open',
      assignedAgent: { id: 'agent', displayName: 'Inactive agent', isEligible: false }, createdAtUtc: '2026-10-08T12:00:00Z', updatedAtUtc: '2026-10-08T12:00:00Z', editVersion: 'AAAAAAAAAAE=' });
    http.expectOne('/api/tickets/TKT-TEST/history?page=1&pageSize=25').flush({ items: [{ id: 'entry', revision: '1', sequence: 1, kind: 'Created', field: null, previousValue: null,
      newValue: { reference: 'TKT-TEST', subject: 'Original subject', description: 'Original description', category: { id: 'category', displayName: 'Original category' }, priority: 'Normal', status: 'Open', assignedAgent: null },
      actor: { issuer: 'https://example.test', subject: 'staff', displayName: 'Original actor' }, occurredAtUtc: '2026-10-08T12:00:00Z' }], page: 1, pageSize: 25, hasMore: false });
    http.expectOne('/api/ticket-reference-data/categories').flush([{ id: 'active', displayName: 'Available' }]);
    http.expectOne('/api/ticket-reference-data/agents').flush([{ id: 'eligible', displayName: 'Eligible' }]);
    await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Inactive category (inactive)'); expect(fixture.nativeElement.textContent).toContain('Inactive agent (inactive)');
    expect(fixture.nativeElement.textContent).toContain('<img src=x onerror=alert(1)>'); expect(fixture.nativeElement.querySelector('img')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Original actor'); expect(fixture.nativeElement.textContent).toContain('Original category');
  });
  it('renders changed string/reference/null snapshots with actor and time at the US3 checkpoint', async () => {
    const fixture = TestBed.createComponent(TicketDetailPage); fixture.detectChanges();
    http.expectOne('/api/tickets/TKT-TEST').flush(sampleTicket);
    const common = { revision: '9007199254740993', sequence: 1, actor: { issuer: 'issuer', subject: 'staff-b', displayName: 'Historical staff' }, occurredAtUtc: '2026-10-08T12:00:00Z' };
    http.expectOne('/api/tickets/TKT-TEST/history?page=1&pageSize=25').flush({ page: 1, pageSize: 25, hasMore: false, items: [
      { ...common, id: 'one', kind: 'DetailsChanged', field: 'subject', previousValue: 'Before text', newValue: '<img> after' },
      { ...common, id: 'two', kind: 'DetailsChanged', field: 'category', previousValue: { id: 'old', displayName: 'Historical category' }, newValue: { id: 'new', displayName: 'Replacement category' } },
      { ...common, id: 'three', kind: 'AssignmentChanged', field: 'assignedAgent', previousValue: { id: 'old-agent', displayName: 'Historical agent' }, newValue: null }
    ] });
    http.expectOne('/api/ticket-reference-data/categories').flush([]); http.expectOne('/api/ticket-reference-data/agents').flush([]);
    await fixture.whenStable(); fixture.detectChanges(); const text = fixture.nativeElement.textContent;
    for (const value of ['9007199254740993', 'Before text', '<img> after', 'Historical category', 'Replacement category', 'Historical agent', 'Unassigned', 'Historical staff']) expect(text).toContain(value);
    expect(fixture.nativeElement.querySelector('img')).toBeNull();
  });
  it('makes closed details and assignment read-only while permitting an explicit reopen', async () => {
    const fixture = TestBed.createComponent(TicketDetailPage); fixture.detectChanges();
    http.expectOne('/api/tickets/TKT-TEST').flush({ ...sampleTicket, status: 'Closed' }); http.expectOne('/api/tickets/TKT-TEST/history?page=1&pageSize=25').flush({ items: [], page: 1, pageSize: 25, hasMore: false });
    http.expectOne('/api/ticket-reference-data/categories').flush([]); http.expectOne('/api/ticket-reference-data/agents').flush([]);
    await fixture.whenStable(); fixture.detectChanges();
    for (const selector of ['#edit-subject', '#edit-description', '#edit-category', '#edit-priority', '#assignment-agent']) expect(fixture.nativeElement.querySelector(selector).matches(':disabled')).toBe(true);
    expect(fixture.nativeElement.querySelector('#ticket-status').matches(':disabled')).toBe(false);
    const status = fixture.nativeElement.querySelector('#ticket-status'); status.value = 'Open'; status.dispatchEvent(new Event('change', { bubbles: true }));
    fixture.nativeElement.querySelector('app-ticket-status form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    const save = http.expectOne('/api/tickets/TKT-TEST/status'); expect(save.request.body.status).toBe('Open'); save.flush({ ...sampleTicket, editVersion: 'AAAAAAAAAAI=' });
    http.expectOne('/api/tickets/TKT-TEST/history?page=1&pageSize=25').flush({ items: [], page: 1, pageSize: 25, hasMore: false }); http.expectOne('/api/ticket-reference-data/categories').flush([]); http.expectOne('/api/ticket-reference-data/agents').flush([]);
    await fixture.whenStable(); fixture.detectChanges(); expect(fixture.nativeElement.querySelector('#edit-subject').matches(':disabled')).toBe(false);
  });
  async function historyReady() {
    const fixture=TestBed.createComponent(TicketDetailPage); fixture.detectChanges(); http.expectOne('/api/tickets/TKT-TEST').flush(sampleTicket);
    http.expectOne('/api/tickets/TKT-TEST/history?page=1&pageSize=25').flush({items:[],page:1,pageSize:25,hasMore:true}); http.expectOne('/api/ticket-reference-data/categories').flush([]); http.expectOne('/api/ticket-reference-data/agents').flush([]); await fixture.whenStable(); fixture.detectChanges(); return fixture;
  }
  it('cancels an obsolete history page request so it cannot replace a newer page',async()=>{
    const fixture=await historyReady(); fixture.componentInstance.loadHistory(2); const obsolete=http.expectOne('/api/tickets/TKT-TEST/history?page=2&pageSize=25'); fixture.componentInstance.loadHistory(3); const latest=http.expectOne('/api/tickets/TKT-TEST/history?page=3&pageSize=25');
    const cancelled=obsolete.cancelled; if(!cancelled) obsolete.flush({items:[],page:2,pageSize:25,hasMore:true}); latest.flush({items:[],page:3,pageSize:25,hasMore:false}); expect(cancelled).toBe(true); expect(fixture.componentInstance.history()!.page).toBe(3);
  });
  it('retries the failed requested page instead of navigating back to the last displayed page',async()=>{
    const fixture=await historyReady(); fixture.componentInstance.loadHistory(2); http.expectOne('/api/tickets/TKT-TEST/history?page=2&pageSize=25').flush({}, {status:503,statusText:'Unavailable'}); fixture.detectChanges();
    const retry=Array.from(fixture.nativeElement.querySelectorAll('app-ticket-history button')).find((button:any)=>button.textContent.includes('Retry history')) as HTMLButtonElement; retry.click(); const requests=http.match(request=>request.url.includes('/history')); const requestedPage=requests[0].request.params.get('page'); for(const request of requests) request.flush({items:[],page:Number(request.request.params.get('page')),pageSize:25,hasMore:false}); expect(requestedPage).toBe('2');
  });
  it('refreshes authoritative history after a saved mutation and cancels the older paged read',async()=>{
    const fixture=await historyReady(); fixture.componentInstance.loadHistory(2); const obsolete=http.expectOne('/api/tickets/TKT-TEST/history?page=2&pageSize=25');
    fixture.componentInstance.session.saveDetails({subject:'Changed subject'}); http.expectOne('/api/tickets/TKT-TEST/details').flush({...sampleTicket,subject:'Changed subject',editVersion:'AAAAAAAAAAI='}); expect(obsolete.cancelled).toBe(true);
    http.expectOne('/api/tickets/TKT-TEST/history?page=1&pageSize=25').flush({items:[{id:'actual-change',revision:'2',sequence:1,kind:'DetailsChanged',field:'subject',previousValue:'Saved',newValue:'Changed subject',actor:{issuer:'issuer',subject:'staff',displayName:'Responsible Staff'},occurredAtUtc:'2026-10-08T10:00:00Z'}],page:1,pageSize:25,hasMore:false}); http.expectOne('/api/ticket-reference-data/categories').flush([]); http.expectOne('/api/ticket-reference-data/agents').flush([]);
    await fixture.whenStable(); fixture.detectChanges(); expect(fixture.nativeElement.querySelector('app-ticket-history').textContent).toContain('Responsible Staff'); expect(fixture.nativeElement.querySelector('app-ticket-history').textContent).toContain('Changed subject');
  });
  it('does not invent history on failed mutations or append local entries to a no-op response',async()=>{
    const fixture=await historyReady(); fixture.componentInstance.session.saveAssignment(null); http.expectOne('/api/tickets/TKT-TEST/assignment').flush({code:'agent_ineligible'},{status:409,statusText:'Conflict'}); http.expectNone('/api/tickets/TKT-TEST/history?page=1&pageSize=25'); expect(fixture.componentInstance.history()!.items).toHaveLength(0);
    fixture.componentInstance.session.saveDetails({subject:sampleTicket.subject}); http.expectOne('/api/tickets/TKT-TEST/details').flush(sampleTicket); http.expectOne('/api/tickets/TKT-TEST/history?page=1&pageSize=25').flush({items:[],page:1,pageSize:25,hasMore:false}); http.expectOne('/api/ticket-reference-data/categories').flush([]); http.expectOne('/api/ticket-reference-data/agents').flush([]); expect(fixture.componentInstance.history()!.items).toHaveLength(0);
  });
  it('cancels previous-ticket history as soon as the route changes, before the next detail loads',async()=>{
    const params=new Subject<ReturnType<typeof convertToParamMap>>(); Object.assign(TestBed.inject(ActivatedRoute),{paramMap:params.asObservable()}); const fixture=TestBed.createComponent(TicketDetailPage); fixture.detectChanges(); params.next(convertToParamMap({reference:'TKT-TEST'}));
    http.expectOne('/api/tickets/TKT-TEST').flush(sampleTicket); const previous=http.expectOne('/api/tickets/TKT-TEST/history?page=1&pageSize=25'); http.expectOne('/api/ticket-reference-data/categories').flush([]); http.expectOne('/api/ticket-reference-data/agents').flush([]);
    params.next(convertToParamMap({reference:'TKT-NEXT'})); expect(previous.cancelled).toBe(true); expect(fixture.componentInstance.history()).toBeNull(); http.expectOne('/api/tickets/TKT-NEXT').flush({...sampleTicket,reference:'TKT-NEXT'});
    http.expectOne('/api/tickets/TKT-NEXT/history?page=1&pageSize=25').flush({items:[],page:1,pageSize:25,hasMore:false}); http.expectOne('/api/ticket-reference-data/categories').flush([]); http.expectOne('/api/ticket-reference-data/agents').flush([]); await fixture.whenStable(); expect(fixture.componentInstance.ticket()!.reference).toBe('TKT-NEXT');
  });
  for (const [status, message] of [[403, 'You do not have shared queue access.'], [404, 'Ticket not found.'], [503, 'Ticket information could not be loaded. Try again.']] as const) {
    it(`distinguishes ${status} from an empty successful result`, async () => {
      const fixture = TestBed.createComponent(TicketDetailPage); fixture.detectChanges(); http.expectOne('/api/tickets/TKT-TEST').flush({}, { status, statusText: 'Error' });
      await fixture.whenStable(); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain(message); http.expectNone('/api/tickets/TKT-TEST/history?page=1&pageSize=25');
    });
  }
});
