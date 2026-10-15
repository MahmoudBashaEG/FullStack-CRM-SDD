import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { ApiUrl } from '../api/api-url';
import { AuthService } from './auth-service';

/** Adds the bearer token to /api/v1 calls; on 401 renews once via the refresh cookie and retries. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const apiUrl = inject(ApiUrl);
  const isBusinessApi = request.url.startsWith(apiUrl.versioned(''));
  const isSessionEndpoint = request.url.startsWith(apiUrl.versioned('auth/'));
  if (!isBusinessApi || isSessionEndpoint) {
    return next(request);
  }

  const withToken = (req: HttpRequest<unknown>) => {
    const token = auth.accessToken();
    return token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  };

  return next(withToken(request)).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }
      return auth.refresh().pipe(
        switchMap(renewed => {
          if (!renewed) {
            auth.endSession();
            return throwError(() => error);
          }
          return next(withToken(request));
        }),
      );
    }),
  );
};
