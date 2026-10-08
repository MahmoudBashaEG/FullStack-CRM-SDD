import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

export const errorInterceptor: HttpInterceptorFn = (request, next) => next(request).pipe(
  catchError((failure: HttpErrorResponse) => {
    const payload = failure.error;
    const code = failure.status === 0 ? 'network_unavailable' : typeof payload?.code === 'string' ? payload.code : 'unexpected_error';
    // Forward stable codes/field errors, never exception internals; no automatic retry.
    return throwError(() => new HttpErrorResponse({ status: failure.status, statusText: failure.statusText, url: failure.url ?? undefined,
      error: { code, ...(payload?.errors && typeof payload.errors === 'object' ? { errors: payload.errors } : {}) } }));
  }),
);
