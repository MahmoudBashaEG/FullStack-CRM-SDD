import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';

export const authInterceptor: HttpInterceptorFn = (request,next) => {
  const url=new URL(request.url,location.origin);
  const isApi=url.origin===location.origin && (url.pathname==='/api' || url.pathname.startsWith('/api/'));
  const publicAuth=['/api/auth/csrf','/api/auth/signin','/api/auth/signup','/api/auth/refresh'].includes(url.pathname);
  if(!isApi || publicAuth) return next(request);
  const auth=inject(AuthService);const token=auth.accessToken();const generation=auth.generation();
  const authenticated=token?request.clone({setHeaders:{Authorization:`Bearer ${token}`}}):request;
  // Ordinary feature/network failures do not invalidate a still-live session.
  // AuthService itself clears every failed refresh/restoration; a protected401
  // remains authoritative. Never replay an uncertain feature mutation.
  const clear=(failure:HttpErrorResponse) => {if(generation===auth.generation() && failure.status===401)auth.loseAuthentication(failure);};
  return next(authenticated).pipe(catchError((failure:HttpErrorResponse) => {
    if(failure.status===401 && failure.error?.code==='token_expired' && token && url.pathname!=='/api/auth/signout') {
      return from(auth.renewAfterExpiry(token,generation)).pipe(switchMap(fresh => {
        if(generation!==auth.generation())return throwError(()=>failure);
        return next(request.clone({setHeaders:{Authorization:`Bearer ${fresh}`}}));
      }),catchError(error=>{clear(error);return throwError(()=>error);}));
    }
    clear(failure);return throwError(()=>failure);
  }));
};
