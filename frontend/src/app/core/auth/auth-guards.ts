import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { UserRole } from './auth-models';
import { AuthService } from './auth-service';

/** Usability only — the API enforces every rule. */
export const authGuard: CanActivateFn = (_route, state) =>
  inject(AuthService).isAuthenticated() ||
  inject(Router).createUrlTree(['/sign-in'], { queryParams: { returnUrl: state.url } });

export const guestGuard: CanActivateFn = () =>
  !inject(AuthService).isAuthenticated() || inject(Router).createUrlTree(['/']);

export function roleGuard(...roles: UserRole[]): CanActivateFn {
  return () => {
    const role = inject(AuthService).role();
    return (role !== null && roles.includes(role)) || inject(Router).createUrlTree(['/']);
  };
}
