import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { TicketDetailPage } from './ticket-detail.page';
import { sampleTicket } from '../testing/ticket-fixtures';

describe('TicketDetailPage', () => {
  let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ imports: [TicketDetailPage], providers: [provideHttpClient(), provideHttpClientTesting(), { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ reference: 'TKT-TEST' })) } }] }); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
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
  for (const [status, message] of [[403, 'You do not have shared queue access.'], [404, 'Ticket not found.'], [503, 'Ticket information could not be loaded. Try again.']] as const) {
    it(`distinguishes ${status} from an empty successful result`, async () => {
      const fixture = TestBed.createComponent(TicketDetailPage); fixture.detectChanges(); http.expectOne('/api/tickets/TKT-TEST').flush({}, { status, statusText: 'Error' });
      await fixture.whenStable(); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain(message); http.expectNone('/api/tickets/TKT-TEST/history?page=1&pageSize=25');
    });
  }
});
