import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const supportQueueGuard: CanActivateFn = () => {
  const auth=inject(AuthService);return auth.allowed() || inject(Router).createUrlTree([auth.authenticated()?'/access':'/auth/signin']);
};
