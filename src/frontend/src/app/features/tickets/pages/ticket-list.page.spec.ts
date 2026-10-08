import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { BehaviorSubject } from 'rxjs';
import { TicketListPage } from './ticket-list.page';
import { AuthService } from '../../../core/auth/auth.service';
import { supportQueueGuard } from '../../../core/auth/support-queue.guard';
import { AccessPage } from '../../../core/auth/access.page';

describe('TicketListPage', () => {
  let params: BehaviorSubject<ParamMap>; let http: HttpTestingController; let allowed: boolean;
  beforeEach(() => {
    allowed = true; params = new BehaviorSubject(convertToParamMap({ q: 'Alpha', status: 'Open', categoryId: 'cat', priority: 'High', assignment: 'unassigned', page: '2', pageSize: '25' }));
    TestBed.configureTestingModule({ imports: [TicketListPage], providers: [provideHttpClient(), provideHttpClientTesting(),
      provideRouter([{ path: 'tickets', component: TicketListPage, canActivate: [supportQueueGuard] }, { path: 'access', component: AccessPage }]),
      { provide: ActivatedRoute, useFactory: () => ({ queryParamMap: params.asObservable(), snapshot: TestBed.inject(Router).routerState.snapshot.root }) },
      { provide: AuthService, useValue: { allowed: () => allowed, authenticated: () => true } }] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function start() {
    const result = TestBed.createComponent(TicketListPage); result.detectChanges();
    http.expectOne('/api/ticket-reference-data/categories').flush([]); http.expectOne('/api/ticket-reference-data/agents').flush([]);
    return result;
  }
  function list() { return http.expectOne(request => request.url === '/api/tickets'); }
  it('preserves every criterion and renders a safe summary with inactive references', async () => {
    const result = start(); const request = list();
    expect(request.request.params.get('q')).toBe('Alpha'); expect(request.request.params.get('status')).toBe('Open');
    expect(request.request.params.get('categoryId')).toBe('cat'); expect(request.request.params.get('priority')).toBe('High'); expect(request.request.params.get('assignment')).toBe('unassigned'); expect(request.request.params.get('page')).toBe('2');
    request.flush({ items: [{ reference: 'TKT-TEST', subject: '<img src=x>', category: { id: 'cat', displayName: 'Old category', isAvailable: false }, priority: 'High', status: 'Open', assignedAgent: null, updatedAtUtc: '2026-10-08T12:00:00Z' }], page: 2, pageSize: 25, hasMore: true });
    await result.whenStable(); result.detectChanges(); expect(result.nativeElement.querySelector('img')).toBeNull();
    expect(result.nativeElement.querySelector('tbody').textContent).toContain('<img src=x>'); expect(result.nativeElement.textContent).toContain('Old category (inactive)');
    expect(result.nativeElement.querySelector('tbody a').getAttribute('href')).toBe('/tickets/TKT-TEST');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true); result.componentInstance.goToPage(3);
    expect(navigate.mock.calls[0][1]?.queryParams).toEqual({ q: 'Alpha', status: 'Open', categoryId: 'cat', priority: 'High', assignment: 'unassigned', page: 3, pageSize: 25 });
  });
  it('distinguishes an empty successful response from loading and failures', async () => {
    const result = start(); expect(result.nativeElement.textContent).toContain('Loading tickets'); list().flush({ items: [], page: 2, pageSize: 25, hasMore: false });
    await result.whenStable(); result.detectChanges(); expect(result.nativeElement.textContent).toContain('No tickets match these filters.'); expect(result.nativeElement.querySelector('[role=alert]')).toBeNull();
  });
  for (const [status, message] of [[400, 'Some filters are invalid.'], [403, 'You do not have shared queue access.'], [503, 'Tickets could not be loaded.']] as const) {
    it(`preserves criteria on ${status} and retries only explicitly`, async () => {
      const result = start(); list().flush({}, { status, statusText: 'Error' }); await result.whenStable(); result.detectChanges();
      expect(result.nativeElement.textContent).toContain(message); expect(result.nativeElement.textContent).not.toContain('No tickets match'); expect(result.componentInstance.filters().q).toBe('Alpha');
      http.expectNone(request => request.url === '/api/tickets'); result.componentInstance.reload(); list().flush({ items: [], page: 2, pageSize: 25, hasMore: false });
    });
  }
  it('resets paging on a filter change and preserves page size', async () => {
    const result = start(); list().flush({ items: [], page: 2, pageSize: 25, hasMore: false });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    result.componentInstance.applyFilters({ q: 'مشكلة', status: '', categoryId: '', priority: '', assignment: 'unassigned' });
    expect(navigate.mock.calls[0][1]?.queryParams).toEqual({ q: 'مشكلة', assignment: 'unassigned', page: 1, pageSize: 25 });
    result.componentInstance.applyFilters({ q: '', status: '', categoryId: '', priority: '', assignment: '' });
    expect(navigate.mock.calls[1][1]?.queryParams).toEqual({ page: 1, pageSize: 25 });
  });
  it('cancels an obsolete request when the URL changes to invalid duplicate criteria', () => {
    const result = start(); const pending = list(); params.next(convertToParamMap({ q: ['one', 'two'] }));
    expect(pending.cancelled).toBe(true); expect(result.componentInstance.error()).toContain('Provide one supported value');
  });
  it('does not reinterpret invalid integer syntax in a deep link', () => {
    const result = start(); list().flush({ items: [], page: 2, pageSize: 25, hasMore: false }); params.next(convertToParamMap({ page: '0x2' }));
    const request = list(); expect(request.request.params.get('page')).toBe('0x2'); request.flush({}, { status: 400, statusText: 'Bad Request' });
    expect(result.componentInstance.error()).toContain('Some filters are invalid.');
  });
  it('denies access before requesting queue data', async () => {
    allowed = false; const harness = await RouterTestingHarness.create(); await harness.navigateByUrl('/tickets');
    expect(TestBed.inject(Router).url).toBe('/access'); http.expectNone(request => request.url.startsWith('/api/'));
  });
});
