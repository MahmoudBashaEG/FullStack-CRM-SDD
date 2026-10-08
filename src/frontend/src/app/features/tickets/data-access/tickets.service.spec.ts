import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { TicketsService } from './tickets.service';
import { authInterceptor } from '../../../core/http/auth.interceptor';
import { AuthService } from '../../../core/auth/auth.service';
import { errorInterceptor } from '../../../core/http/error.interceptor';

describe('TicketsService', () => {
  let service: TicketsService; let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])), provideHttpClientTesting(), { provide: AuthService, useValue: { accessToken: () => 'synthetic-token' } }] });
    service = TestBed.inject(TicketsService); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it('posts only writable creation input and includes credentials for this API', () => {
    service.create({ subject: 'Subject', description: 'Description' }).subscribe();
    const request = http.expectOne('/api/tickets'); expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ subject: 'Subject', description: 'Description' });
    expect(request.request.headers.get('Authorization')).toBe('Bearer synthetic-token'); request.flush({});
  });
  it('reads history with bounded paging and keeps revision strings opaque', () => {
    let revision: string | undefined;
    service.history('TKT-ABC', 2).subscribe(page => revision = page.items[0].revision);
    const request = http.expectOne('/api/tickets/TKT-ABC/history?page=2&pageSize=25');
    request.flush({ items: [{ revision: '9007199254740993' }], page: 2, pageSize: 25, hasMore: false });
    expect(revision).toBe('9007199254740993');
  });
  it('does not send credentials to another origin', () => {
    TestBed.inject(HttpClient).get('https://other.example/api/tickets').subscribe();
    const request = http.expectOne('https://other.example/api/tickets');
    expect(request.request.headers.has('Authorization')).toBe(false); request.flush({});
  });
  it('retains stable error codes and removes diagnostic details without replay', () => {
    let failure: any;
    service.detail('TKT-TEST').subscribe({ error: error => failure = error });
    http.expectOne('/api/tickets/TKT-TEST').flush({ code: 'service_unavailable', detail: 'Internal connection details', stack: 'internal stack' }, { status: 503, statusText: 'Unavailable' });
    expect(failure.status).toBe(503); expect(failure.error).toEqual({ code: 'service_unavailable' }); http.expectNone('/api/tickets/TKT-TEST');
  });
  it('reports network uncertainty without replaying creation', () => {
    let failure: any;
    service.create({ subject: 'S', description: 'D' }).subscribe({ error: error => failure = error });
    http.expectOne('/api/tickets').error(new ProgressEvent('error'));
    expect(failure.status).toBe(0); expect(failure.error.code).toBe('network_unavailable'); http.expectNone('/api/tickets');
  });
});
