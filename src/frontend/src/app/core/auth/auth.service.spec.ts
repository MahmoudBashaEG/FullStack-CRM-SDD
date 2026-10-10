import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { AuthService } from './auth.service';
import { HttpErrorResponse } from '@angular/common/http';

describe('local auth state and restoration', () => {
  it.each(['\ufeffuser@example.test','\ufeffuser@example.test\ufeff','\u0085"multi part"@example.test\u0085'])('signin preserves the canonical identifier %j and publishes its own stable identity',async input=>{
    const canonical=input.replace(/^\p{White_Space}+|\p{White_Space}+$/gu,'');const expectedId='20000000-0000-0000-0000-000000000002';
    const pending=auth.signIn(input,'  exact password  ');for(let i=0;i<8;i++)await Promise.resolve();http.expectOne('/api/auth/csrf').flush({requestToken:'csrf'});for(let i=0;i<8;i++)await Promise.resolve();
    const signin=http.expectOne('/api/auth/signin');const sent=signin.request.body.email;expect(signin.request.body.password).toBe('  exact password  ');signin.flush(envelope);
    for(let i=0;i<8;i++)await Promise.resolve();http.expectOne('/api/auth/me').flush({...identity,userId:sent===canonical?expectedId:identity.userId,role:'Customer'});
    for(let i=0;i<8;i++)await Promise.resolve();http.expectOne('/api/auth/csrf').flush({requestToken:'next'});await pending;
    expect(sent).toBe(canonical);expect(auth.identity()?.userId).toBe(expectedId);
  });
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
  it.each([200,503,0])('drains invalidated refresh before newer signin, including late %i results',async status=>{
    await restore();const old=auth.refresh();const waiter=auth.refresh();const rejected=Promise.all([expect(old).rejects.toBeDefined(),expect(waiter).rejects.toBeDefined()]);await Promise.resolve();const pending=http.expectOne('/api/auth/refresh');
    auth.loseAuthentication(new HttpErrorResponse({status:401}));const signin=auth.signIn('next@example.test','  exact password  ');
    for(let i=0;i<15;i++)await Promise.resolve();const early=http.match('/api/auth/signin');
    if(status===200)pending.flush({...envelope,accessToken:'late-old'});else if(status===0)pending.error(new ProgressEvent('error'));else pending.flush({code:'service_unavailable'},{status,statusText:'Unavailable'});
    await rejected;for(let i=0;i<15;i++)await Promise.resolve();const request=early[0]??http.expectOne('/api/auth/signin');request.flush({...envelope,accessToken:'new-session'});
    for(let i=0;i<5;i++)await Promise.resolve();http.expectOne('/api/auth/me').flush({...identity,displayName:'New session',role:'Customer'});
    for(let i=0;i<5;i++)await Promise.resolve();http.expectOne('/api/auth/csrf').flush({requestToken:'new-csrf'});await signin;
    expect(early).toHaveLength(0);expect(auth.accessToken()).toBe('new-session');expect(auth.identity()?.displayName).toBe('New session');expect(auth.status()).toBe('authenticated');http.expectNone('/api/auth/me');
  });
  it('drains refresh after invalidation before signout and permits later explicit recovery',async()=>{
    await restore();const pendingOperation=auth.refresh();const rejected=expect(pendingOperation).rejects.toBeDefined();await Promise.resolve();const pending=http.expectOne('/api/auth/refresh');auth.loseAuthentication(new HttpErrorResponse({status:401}));const signout=auth.signOut();
    for(let i=0;i<15;i++)await Promise.resolve();const early=http.match('/api/auth/signout');pending.flush(envelope);await rejected;for(let i=0;i<15;i++)await Promise.resolve();(early[0]??http.expectOne('/api/auth/signout')).flush(null,{status:204,statusText:'No Content'});
    for(let i=0;i<5;i++)await Promise.resolve();http.expectOne('/api/auth/csrf').flush({requestToken:'next'});await signout;expect(early).toHaveLength(0);expect(auth.authenticated()).toBe(false);
    const recovery=auth.refresh();await Promise.resolve();http.expectOne('/api/auth/refresh').flush({...envelope,accessToken:'recovered'});await Promise.resolve();await Promise.resolve();http.expectOne('/api/auth/me').flush(identity);await recovery;expect(auth.accessToken()).toBe('recovered');
  });
  it('explicit recovery during invalidated transport joins it without starting another rotation',async()=>{
    await restore();const first=auth.refresh();const firstRejected=expect(first).rejects.toBeDefined();await Promise.resolve();const pending=http.expectOne('/api/auth/refresh');
    auth.loseAuthentication(new HttpErrorResponse({status:401}));const recovery=auth.refresh();const recoveryRejected=expect(recovery).rejects.toBeDefined();
    for(let i=0;i<8;i++)await Promise.resolve();http.expectNone('/api/auth/refresh');pending.flush({...envelope,accessToken:'stale'});
    await Promise.all([firstRejected,recoveryRejected]);expect(auth.accessToken()).toBeNull();expect(auth.identity()).toBeNull();http.expectNone('/api/auth/me');
    const fresh=auth.refresh();await Promise.resolve();http.expectOne('/api/auth/refresh').flush({...envelope,accessToken:'fresh'});await Promise.resolve();await Promise.resolve();http.expectOne('/api/auth/me').flush(identity);await fresh;expect(auth.accessToken()).toBe('fresh');
  });
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
  it.each([503,0])('rejects a late old-token expiry after a failed %i refresh without another rotation',async status=>{
    await restore();const generation=auth.generation();const failed=auth.renewAfterExpiry(envelope.accessToken,generation);const rejection=expect(failed).rejects.toBeDefined();await Promise.resolve();
    const pending=http.expectOne('/api/auth/refresh');if(status===0)pending.error(new ProgressEvent('error'));else pending.flush({code:'service_unavailable'},{status,statusText:'Unavailable'});await rejection;
    const late=auth.renewAfterExpiry(envelope.accessToken,generation);const lateRejection=expect(late).rejects.toBeDefined();await Promise.resolve();await Promise.resolve();
    // Drain an unexpected request so the failing-first run is a precise behavioral failure.
    const repeated=http.match('/api/auth/refresh');for(const request of repeated)request.flush({code:'service_unavailable'},{status:503,statusText:'Unavailable'});await lateRejection;
    expect(repeated).toHaveLength(0);expect(auth.accessToken()).toBeNull();expect(auth.identity()).toBeNull();expect(auth.status()).toBe('unavailable');
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
