import { HttpBackend, HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CrmIdentity, TokenEnvelope } from './auth.models';

@Injectable({providedIn:'root'})
export class AuthTransport {
  private readonly http: HttpClient;
  constructor(backend: HttpBackend) { this.http = new HttpClient(backend); }
  csrf() { return firstValueFrom(this.http.get<{requestToken:string}>('/api/auth/csrf')); }
  signIn(email:string,password:string,csrf:string) { return firstValueFrom(this.http.post<TokenEnvelope>('/api/auth/signin',{email,password},{headers:{'X-CSRF-TOKEN':csrf},withCredentials:true})); }
  refresh(csrf:string) { return firstValueFrom(this.http.post<TokenEnvelope>('/api/auth/refresh',{}, {headers:{'X-CSRF-TOKEN':csrf},withCredentials:true})); }
  me(token:string) { return firstValueFrom(this.http.get<CrmIdentity>('/api/auth/me',{headers:{Authorization:`Bearer ${token}`}})); }
  signOut(csrf:string,token:string|null) { return firstValueFrom(this.http.post<void>('/api/auth/signout',{}, {headers:{'X-CSRF-TOKEN':csrf,...(token?{Authorization:`Bearer ${token}`}:{})},withCredentials:true})); }
}
