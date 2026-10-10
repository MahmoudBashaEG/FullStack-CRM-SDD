import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../auth/auth.service';
import { authInterceptor } from './auth.interceptor';

describe('local bearer and bounded retry', () => {
  let client: HttpClient; let http: HttpTestingController; let token: string;
  const renew = vi.fn(async () => { token = 'new'; return token; }); const lose = vi.fn();
  beforeEach(() => { token = 'old'; renew.mockClear(); lose.mockClear(); TestBed.configureTestingModule({providers:[provideHttpClient(withInterceptors([authInterceptor])),provideHttpClientTesting(),{provide:AuthService,useValue:{accessToken:()=>token,generation:()=>1,renewAfterExpiry:renew,loseAuthentication:lose}}]}); client=TestBed.inject(HttpClient);http=TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  it.each([503,0])('keeps a valid JWT for explicit Ticket recovery after %i, without replaying the uncertain operation',async status=>{
    const result=firstValueFrom(client.patch('/api/tickets/TKT/details',{expectedVersion:'version',subject:'Draft'}));const rejected=expect(result).rejects.toBeDefined();const request=http.expectOne('/api/tickets/TKT/details');if(status===0)request.error(new ProgressEvent('error'));else request.flush({code:'service_unavailable'},{status,statusText:'Unavailable'});await rejected;
    expect(lose).not.toHaveBeenCalled();expect(renew).not.toHaveBeenCalled();http.expectNone('/api/tickets/TKT/details');
    const reload=firstValueFrom(client.get('/api/tickets/TKT'));const read=http.expectOne('/api/tickets/TKT');expect(read.request.headers.get('Authorization')).toBe('Bearer old');read.flush({subject:'Actually committed'});await reload;
  });
  it('replays an expired request once using the published JWT', async () => {
    const result=firstValueFrom(client.get('/api/tickets'));const old=http.expectOne('/api/tickets');expect(old.request.headers.get('Authorization')).toBe('Bearer old');old.flush({code:'token_expired'},{status:401,statusText:'Unauthorized'});
    await Promise.resolve();await Promise.resolve();const replay=http.expectOne('/api/tickets');expect(replay.request.headers.get('Authorization')).toBe('Bearer new');replay.flush({ok:true});await result;expect(renew).toHaveBeenCalledTimes(1);
  });
  it('does not attach JWT outside same-origin API or public auth transport', async () => {
    for(const url of ['https://outside.test/api/tickets','/api-docs','/api/auth/csrf','/api/auth/signin','/api/auth/refresh','/api/auth/signup']){
      const result=firstValueFrom(client.get(url));const request=http.expectOne(url);expect(request.request.headers.has('Authorization')).toBe(false);request.flush({});await result;
    }
  });
  it('does not retry forbidden, conflict, revoked or network-failed operations', async () => {
    for(const [status,code] of [[403,'forbidden'],[409,'ticket_changed'],[401,'authentication_invalid'],[503,'service_unavailable']] as const){
      const result=firstValueFrom(client.post('/api/tickets',{}));const rejected=expect(result).rejects.toBeDefined();http.expectOne('/api/tickets').flush({code},{status,statusText:'Failure'});await rejected;
    }
    expect(renew).not.toHaveBeenCalled();expect(lose).toHaveBeenCalled();
  });
  it('never loops when the replay is also unauthorized', async () => {
    const result=firstValueFrom(client.get('/api/tickets'));const rejected=expect(result).rejects.toBeDefined();http.expectOne('/api/tickets').flush({code:'token_expired'},{status:401,statusText:'Unauthorized'});
    await Promise.resolve();await Promise.resolve();http.expectOne('/api/tickets').flush({code:'token_expired'},{status:401,statusText:'Unauthorized'});await rejected;expect(renew).toHaveBeenCalledTimes(1);expect(lose).toHaveBeenCalled();
  });
});
