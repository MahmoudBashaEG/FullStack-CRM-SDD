import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TicketEditSession } from './ticket-edit-session.service';
import { sampleTicket } from '../testing/ticket-fixtures';
describe('TicketEditSession', () => {
  let session: TicketEditSession; let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ providers: [TicketEditSession, provideHttpClient(), provideHttpClientTesting()] }); session = TestBed.inject(TicketEditSession); http = TestBed.inject(HttpTestingController); session.accept(sampleTicket); });
  afterEach(() => http.verify());
  it('keeps the draft seed on conflict, explicitly reloads/reviews and never automatically replays', () => {
    session.saveDetails({ subject: 'Draft' }); const save = http.expectOne('/api/tickets/TKT-TEST/details'); expect(save.request.method).toBe('PATCH'); expect(save.request.body).toEqual({ expectedVersion: sampleTicket.editVersion, subject: 'Draft' }); expect(save.request.headers.has('If-Match')).toBe(false);
    save.flush({ code: 'ticket_changed' }, { status: 409, statusText: 'Conflict' }); expect(session.seed()).toBe(sampleTicket); expect(session.blocked()).toBe(true);
    session.saveDetails({ subject: 'Draft' }); http.expectNone('/api/tickets/TKT-TEST/details');
    session.reload(); http.expectOne('/api/tickets/TKT-TEST').flush({ ...sampleTicket, subject: 'Other writer', editVersion: 'AAAAAAAAAAI=' });
    expect(session.seed()).toBe(sampleTicket); expect(session.detail()!.subject).toBe('Other writer'); expect(session.blocked()).toBe(true); http.expectNone('/api/tickets/TKT-TEST/details');
    session.confirmReview(); session.saveDetails({ subject: 'Draft' }); const retry = http.expectOne('/api/tickets/TKT-TEST/details'); expect(retry.request.body.expectedVersion).toBe('AAAAAAAAAAI='); retry.flush({ ...sampleTicket, subject: 'Draft', editVersion: 'AAAAAAAAAAM=' }); expect(session.seed()!.subject).toBe('Draft'); expect(session.blocked()).toBe(false);
  });
  it('clears assignment with explicit null and keeps a no-op response token unchanged', () => {
    session.saveAssignment(null); const save = http.expectOne('/api/tickets/TKT-TEST/assignment'); expect(save.request.method).toBe('PUT'); expect(save.request.body).toEqual({ expectedVersion: sampleTicket.editVersion, agentId: null }); save.flush(sampleTicket); expect(session.detail()!.editVersion).toBe(sampleTicket.editVersion);
  });
  for (const code of ['category_unavailable', 'agent_ineligible', 'ticket_closed']) it(`distinguishes ${code} from stale conflict`, () => {
    session.saveAssignment('new'); http.expectOne('/api/tickets/TKT-TEST/assignment').flush({ code }, { status: 409, statusText: 'Conflict' }); expect(session.needsReload()).toBe(false); expect(session.seed()).toBe(sampleTicket); expect(session.message()).not.toContain('Another staff');
  });
  it('blocks deliberate retry after a lost response until explicit reload/review', () => {
    session.saveDetails({ subject: 'Draft' }); http.expectOne('/api/tickets/TKT-TEST/details').error(new ProgressEvent('error')); expect(session.needsReload()).toBe(true); expect(session.message()).toContain('uncertain'); expect(session.seed()).toBe(sampleTicket); session.saveDetails({ subject: 'Draft' }); http.expectNone('/api/tickets/TKT-TEST/details');
  });
  it('disallows mutation on closed current state even after review', () => { session.accept({ ...sampleTicket, status: 'Closed' }); session.saveAssignment(null); http.expectNone('/api/tickets/TKT-TEST/assignment'); });
  it('does not let a response from the previous ticket overwrite the current route session', () => {
    session.saveDetails({ subject: 'Old draft' }); const oldSave = http.expectOne('/api/tickets/TKT-TEST/details');
    session.reset(); session.accept({ ...sampleTicket, reference: 'TKT-NEXT', subject: 'Next issue' });
    oldSave.flush({ ...sampleTicket, subject: 'Old draft', editVersion: 'AAAAAAAAAAI=' });
    expect(session.detail()!.reference).toBe('TKT-NEXT'); expect(session.seed()!.subject).toBe('Next issue'); expect(session.busy()).toBe(false);
  });
});
