import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { AuthService } from './auth.service';

describe('local auth state and restoration', () => {
  let auth: AuthService; let http: HttpTestingController;
  const identity = { userId: '10000000-0000-0000-0000-000000000001', displayName: 'Admin', role: 'Admin' };
  const envelope = { accessToken: 'private-memory-token', tokenType: 'Bearer', expiresAtUtc: new Date(Date.now() + 300000).toISOString() };
  beforeEach(() => { TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }); auth = TestBed.inject(AuthService); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  async function restore() {
    const first = auth.initialize(); const second = auth.refresh();
    http.expectOne('/api/auth/csrf').flush({ requestToken: 'csrf' }); await Promise.resolve(); await Promise.resolve();
    http.expectOne('/api/auth/refresh').flush(envelope); await Promise.resolve(); await Promise.resolve();
    const me = http.expectOne('/api/auth/me'); expect(me.request.headers.get('Authorization')).toBe('Bearer private-memory-token'); me.flush(identity);
    await Promise.all([first, second]);
  }
  it('restores identity and joins refresh only until identity is published', async () => {
    expect(auth.authenticated()).toBe(false); await restore(); expect(auth.authenticated()).toBe(true); expect(auth.allowed()).toBe(true); expect(auth.accessToken()).toBe(envelope.accessToken);
    expect(localStorage.getItem('accessToken')).toBeNull(); expect(sessionStorage.getItem('accessToken')).toBeNull();
  });
  it('clears JWT/identity on refresh failure and has no automatic loop', async () => {
    await restore(); const refresh = auth.refresh(); const rejection = expect(refresh).rejects.toBeDefined(); await Promise.resolve();
    http.expectOne('/api/auth/refresh').flush({ code: 'service_unavailable' }, { status: 503, statusText: 'Unavailable' }); await rejection;
    expect(auth.accessToken()).toBeNull(); expect(auth.authenticated()).toBe(false); expect(auth.status()).toBe('unavailable');
  });
  it('clears restored identity when me fails', async () => {
    const initialization = auth.initialize(); http.expectOne('/api/auth/csrf').flush({ requestToken: 'csrf' }); await Promise.resolve(); await Promise.resolve();
    http.expectOne('/api/auth/refresh').flush(envelope); await Promise.resolve(); await Promise.resolve();
    http.expectOne('/api/auth/me').flush({code:'authentication_invalid'},{status:401,statusText:'Unauthorized'}); await initialization;
    expect(auth.accessToken()).toBeNull(); expect(auth.authenticated()).toBe(false);
  });
  it('ignores a late refresh completion after successful signout', async () => {
    await restore(); const refresh = auth.refresh(); const rejection = expect(refresh).rejects.toBeDefined(); await Promise.resolve(); const pending = http.expectOne('/api/auth/refresh');
    const signout = auth.signOut(); await Promise.resolve(); http.expectNone('/api/auth/signout');
    pending.flush(envelope); await rejection;
    for(let i=0;i<10;i++)await Promise.resolve();http.expectOne('/api/auth/signout').flush(null,{status:204,statusText:'No Content'});
    await Promise.resolve(); await Promise.resolve(); http.expectOne('/api/auth/csrf').flush({requestToken:'next-csrf'}); await signout;
    expect(auth.accessToken()).toBeNull(); expect(auth.authenticated()).toBe(false);
  });
  it('reuses an already renewed JWT for a late old-token expiry response',async()=>{
    await restore();const generation=auth.generation();const renewed=auth.refresh();await Promise.resolve();http.expectOne('/api/auth/refresh').flush({...envelope,accessToken:'renewed'});await Promise.resolve();await Promise.resolve();http.expectOne('/api/auth/me').flush(identity);await renewed;
    expect(await auth.renewAfterExpiry(envelope.accessToken,generation)).toBe('renewed');http.expectNone('/api/auth/refresh');
  });
  it('rejects every same-instance waiter and clears state when identity restoration fails',async()=>{
    await restore();const first=auth.refresh();const second=auth.refresh();const rejected=Promise.all([expect(first).rejects.toBeDefined(),expect(second).rejects.toBeDefined()]);await Promise.resolve();http.expectOne('/api/auth/refresh').flush(envelope);await Promise.resolve();await Promise.resolve();http.expectOne('/api/auth/me').flush({role:'Unsupported'});await rejected;expect(auth.accessToken()).toBeNull();expect(auth.identity()).toBeNull();expect(auth.status()).toBe('unavailable');
  });
  it('rejects a malformed token envelope before sending any bearer request',async()=>{
    await restore();const pending=auth.refresh();const rejected=expect(pending).rejects.toBeDefined();await Promise.resolve();http.expectOne('/api/auth/refresh').flush({...envelope,accessToken:''});await Promise.resolve();await Promise.resolve();
    const unexpected=http.match('/api/auth/me');for(const request of unexpected)request.flush({code:'authentication_invalid'},{status:401,statusText:'Unauthorized'});await rejected;
    expect(unexpected.length).toBe(0);expect(auth.accessToken()).toBeNull();expect(auth.status()).toBe('unavailable');
  });
  it('signup sends only Customer inputs with CSRF and preserves the current authenticated context',async()=>{
    await restore();const generation=auth.generation();const registration=auth.registerCustomer('Customer','customer@fixture.test','  عربي password  ');
    for(let i=0;i<8;i++)await Promise.resolve();const request=http.expectOne('/api/auth/signup');
    expect(request.request.body).toEqual({displayName:'Customer',email:'customer@fixture.test',password:'  عربي password  '});expect(request.request.headers.get('Authorization')).toBeNull();expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('csrf');expect(request.request.withCredentials).toBe(true);
    request.flush({message:'Registration complete. Sign in to continue.'},{status:201,statusText:'Created'});await registration;
    expect(auth.generation()).toBe(generation);expect(auth.accessToken()).toBe(envelope.accessToken);expect(auth.identity()).toEqual(identity);http.expectNone('/api/auth/me');http.expectNone('/api/auth/refresh');
  });
  it('anonymous signup success or rejection never authenticates or retries',async()=>{
    const registration=auth.registerCustomer('Customer','customer@fixture.test','123456789012345');
    for(let i=0;i<8;i++)await Promise.resolve();http.expectOne('/api/auth/csrf').flush({requestToken:'csrf'});await Promise.resolve();await Promise.resolve();
    http.expectOne('/api/auth/signup').flush({message:'Registration complete. Sign in to continue.'},{status:201,statusText:'Created'});await registration;expect(auth.accessToken()).toBeNull();expect(auth.identity()).toBeNull();
    const duplicate=auth.registerCustomer('Customer','customer@fixture.test','123456789012345');const rejected=expect(duplicate).rejects.toBeDefined();for(let i=0;i<8;i++)await Promise.resolve();http.expectOne('/api/auth/signup').flush({code:'registration_failed'},{status:400,statusText:'Bad Request'});await rejected;expect(auth.authenticated()).toBe(false);http.expectNone('/api/auth/refresh');
  });
});
