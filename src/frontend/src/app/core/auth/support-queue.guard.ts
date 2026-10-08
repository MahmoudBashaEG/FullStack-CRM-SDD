import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const supportQueueGuard: CanActivateFn = () => inject(AuthService).allowed() || inject(Router).createUrlTree(['/access']);
