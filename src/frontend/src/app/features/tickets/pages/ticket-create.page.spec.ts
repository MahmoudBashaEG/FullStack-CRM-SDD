import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { RouterTestingHarness } from '@angular/router/testing';
import { supportQueueGuard } from '../../../core/auth/support-queue.guard';
import { AuthService } from '../../../core/auth/auth.service';
import { AccessPage } from '../../../core/auth/access.page';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { By } from '@angular/platform-browser';
import { TicketCreatePage } from './ticket-create.page';
import { TicketCreateForm } from '../components/ticket-create-form.component';
import { FALLBACK_CATEGORY } from '../models/ticket.models';

describe('TicketCreatePage', () => {
  let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ imports: [TicketCreatePage], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), { provide: AuthService, useValue: { allowed: () => false, authenticated: () => true, identity: () => null, status: () => 'authenticated' } }] }); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  async function ready() {
    const fixture = TestBed.createComponent(TicketCreatePage); fixture.detectChanges();
    http.expectOne('/api/ticket-reference-data/categories').flush([{ id: FALLBACK_CATEGORY, displayName: 'Uncategorised' }]);
    await fixture.whenStable(); fixture.detectChanges(); return fixture;
  }
  it('uses defaults and rejects whitespace-only required input before HTTP', async () => {
    const fixture = await ready(); const form = fixture.debugElement.query(By.directive(TicketCreateForm)).componentInstance as TicketCreateForm;
    expect(form.form.getRawValue().categoryId).toBe(FALLBACK_CATEGORY); expect(form.form.getRawValue().priority).toBe('Normal');
    form.form.patchValue({ subject: '  ', description: 'Description' }); form.submit(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Subject is required.'); http.expectNone('/api/tickets');
  });
  it('navigates to the server reference only after successful save', async () => {
    const fixture = await ready(); const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fixture.componentInstance.create({ subject: 'مشكلة', description: 'وصف' });
    expect(navigate).not.toHaveBeenCalled(); http.expectOne('/api/tickets').flush({ reference: 'TKT-SAVED' });
    expect(navigate).toHaveBeenCalledWith(['/tickets', 'TKT-SAVED']);
  });
  it('preserves input and exposes field errors without automatically replaying a failed save', async () => {
    const fixture = await ready(); const form = fixture.debugElement.query(By.directive(TicketCreateForm)).componentInstance as TicketCreateForm;
    form.form.patchValue({ subject: 'Original subject', description: 'Original description' }); form.submit();
    http.expectOne('/api/tickets').flush({ code: 'validation_failed', errors: { subject: ['Server validation failed.'] } }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges(); expect(form.form.getRawValue().subject).toBe('Original subject'); expect(fixture.nativeElement.textContent).toContain('Server validation failed.');
    expect(fixture.componentInstance.saving()).toBe(false); http.expectNone('/api/tickets');
  });
  it('distinguishes category loading failure and supports explicit retry', async () => {
    const fixture = TestBed.createComponent(TicketCreatePage); fixture.detectChanges();
    http.expectOne('/api/ticket-reference-data/categories').flush({}, { status: 503, statusText: 'Unavailable' }); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Categories could not be loaded.'); expect(fixture.componentInstance.ready()).toBe(false);
    fixture.componentInstance.loadCategories(); http.expectOne('/api/ticket-reference-data/categories').flush([{ id: FALLBACK_CATEGORY, displayName: 'Uncategorised' }]);
    await fixture.whenStable(); expect(fixture.componentInstance.ready()).toBe(true);
  });
  it('redirects denied staff before loading protected feature data', async () => {
    const router = TestBed.inject(Router);
    router.resetConfig([{ path: 'tickets/new', component: TicketCreatePage, canActivate: [supportQueueGuard] }, { path: 'access', component: AccessPage }]);
    const harness = await RouterTestingHarness.create(); await harness.navigateByUrl('/tickets/new');
    expect(router.url).toBe('/access'); http.expectNone('/api/ticket-reference-data/categories');
  });
});
