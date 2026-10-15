import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/auth/auth-guards';
import { Shell } from './core/layout/shell/shell';

export const routes: Routes = [
  {
    path: 'sign-in',
    title: 'Sign in | Support CRM',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/sign-in-page').then(m => m.SignInPage),
  },
  {
    path: 'sign-up',
    title: 'Create account | Support CRM',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/sign-up-page').then(m => m.SignUpPage),
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', loadChildren: () => import('./features/home/home.routes').then(m => m.HOME_ROUTES) },
      { path: 'profile', loadChildren: () => import('./features/profile/profile.routes').then(m => m.PROFILE_ROUTES) },
      {
        path: 'agents',
        canActivate: [roleGuard('SuperAdmin')],
        loadChildren: () => import('./features/agents/agents.routes').then(m => m.AGENTS_ROUTES),
      },
      // Feature stories add: { path: '<feature>', loadChildren: () => import('./features/<feature>/<feature>.routes') ... }
    ],
  },
  { path: '**', redirectTo: '' },
];
