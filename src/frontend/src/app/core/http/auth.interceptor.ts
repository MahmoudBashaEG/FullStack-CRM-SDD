import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { AuthService } from '../auth/auth.service';
import { environment } from '../../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const api = new URL(environment.apiBaseUrl, location.origin);
  const url = new URL(request.url, location.origin);
  const token = inject(AuthService).accessToken();
  const isApi = url.origin === api.origin && (url.pathname === api.pathname || url.pathname.startsWith(api.pathname.replace(/\/$/, '') + '/'));
  return next(token && isApi ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request);
};
