import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { TicketDetailPage } from './ticket-detail.page';

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
    await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Inactive category (inactive)'); expect(fixture.nativeElement.textContent).toContain('Inactive agent (inactive)');
    expect(fixture.nativeElement.textContent).toContain('<img src=x onerror=alert(1)>'); expect(fixture.nativeElement.querySelector('img')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Original actor'); expect(fixture.nativeElement.textContent).toContain('Original category');
  });
  for (const [status, message] of [[403, 'You do not have shared queue access.'], [404, 'Ticket not found.'], [503, 'Ticket information could not be loaded. Try again.']] as const) {
    it(`distinguishes ${status} from an empty successful result`, async () => {
      const fixture = TestBed.createComponent(TicketDetailPage); fixture.detectChanges(); http.expectOne('/api/tickets/TKT-TEST').flush({}, { status, statusText: 'Error' });
      await fixture.whenStable(); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain(message); http.expectNone('/api/tickets/TKT-TEST/history?page=1&pageSize=25');
    });
  }
});
