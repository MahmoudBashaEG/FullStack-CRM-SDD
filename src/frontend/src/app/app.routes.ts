import { Routes } from '@angular/router';
import { supportQueueGuard } from './core/auth/support-queue.guard';

export const routes: Routes = [
  { path: 'tickets', canActivate: [supportQueueGuard], canActivateChild: [supportQueueGuard], loadChildren: () => import('./features/tickets/tickets.routes').then(module => module.ticketsRoutes) },
  { path: 'access', loadComponent: () => import('./core/auth/access.page').then(module => module.AccessPage) },
  { path: 'auth/callback', redirectTo: 'tickets/new' },
  { path: '', pathMatch: 'full', redirectTo: 'tickets/new' },
  { path: '**', redirectTo: 'tickets/new' },
];
