import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../api/api-url';
import { AuthSessionResponse, CurrentUser, LoginRequest, RegisterCustomerRequest } from './auth-models';

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  login(body: LoginRequest): Observable<AuthSessionResponse> {
    return this.http.post<AuthSessionResponse>(this.apiUrl.versioned('auth/login'), body);
  }

  register(body: RegisterCustomerRequest): Observable<CurrentUser> {
    return this.http.post<CurrentUser>(this.apiUrl.versioned('auth/register'), body);
  }

  refresh(): Observable<AuthSessionResponse> {
    return this.http.post<AuthSessionResponse>(this.apiUrl.versioned('auth/refresh'), null);
  }

  logout(): Observable<void> {
    return this.http.post<void>(this.apiUrl.versioned('auth/logout'), null);
  }
}
