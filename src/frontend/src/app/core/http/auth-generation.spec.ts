import {TestBed} from '@angular/core/testing';
import {HttpClient,provideHttpClient,withInterceptors} from '@angular/common/http';
import {HttpTestingController,provideHttpClientTesting} from '@angular/common/http/testing';
import {firstValueFrom} from 'rxjs';
import {describe,it,expect} from 'vitest';
import {AuthService} from '../auth/auth.service';
import {authInterceptor} from './auth.interceptor';

describe('protected401 invalidation during refresh',()=>{
  it.each([200,503])('drains late %i refresh before signin and rejects old request replay',async status=>{
    TestBed.configureTestingModule({providers:[provideHttpClient(withInterceptors([authInterceptor])),provideHttpClientTesting()]});
    const auth=TestBed.inject(AuthService),http=TestBed.inject(HttpTestingController),client=TestBed.inject(HttpClient);
    const envelope=(token:string)=>({accessToken:token,tokenType:'Bearer',expiresAtUtc:new Date(Date.now()+300000).toISOString()});
    const identity={userId:'10000000-0000-0000-0000-000000000001',displayName:'Old',role:'Agent'};
    const tick=async()=>{for(let i=0;i<15;i++)await Promise.resolve();};
    const initialization=auth.initialize();http.expectOne('/api/auth/csrf').flush({requestToken:'csrf'});await tick();http.expectOne('/api/auth/refresh').flush(envelope('old'));await tick();http.expectOne('/api/auth/me').flush(identity);await initialization;
    const expired=firstValueFrom(client.get('/api/tickets?search=old'));const invalid=firstValueFrom(client.get('/api/ticket-reference-data/categories'));
    const rejected=Promise.all([expect(expired).rejects.toBeDefined(),expect(invalid).rejects.toBeDefined()]);
    http.expectOne('/api/tickets?search=old').flush({code:'token_expired'},{status:401,statusText:'Unauthorized'});await tick();const pending=http.expectOne('/api/auth/refresh');
    http.expectOne('/api/ticket-reference-data/categories').flush({code:'authentication_invalid'},{status:401,statusText:'Unauthorized'});await tick();expect(auth.accessToken()).toBeNull();
    const signin=auth.signIn('new@example.test','exact password value');await tick();const early=http.match('/api/auth/signin');
    if(status===200)pending.flush(envelope('late-old'));else pending.flush({code:'service_unavailable'},{status,statusText:'Unavailable'});
    await rejected;await tick();(early[0]??http.expectOne('/api/auth/signin')).flush(envelope('new'));await tick();http.expectOne('/api/auth/me').flush({...identity,displayName:'New',role:'Customer'});await tick();http.expectOne('/api/auth/csrf').flush({requestToken:'next'});await signin;
    expect(early).toHaveLength(0);expect(auth.accessToken()).toBe('new');expect(auth.identity()?.displayName).toBe('New');http.expectNone('/api/tickets?search=old');http.verify();
  });
});
