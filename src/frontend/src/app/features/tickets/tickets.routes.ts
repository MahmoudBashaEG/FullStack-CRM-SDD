import { Routes } from '@angular/router';
export const ticketsRoutes: Routes = [
  { path: 'new', loadComponent: () => import('./pages/ticket-create.page').then(module => module.TicketCreatePage) },
  { path: ':reference', loadComponent: () => import('./pages/ticket-detail.page').then(module => module.TicketDetailPage) },
];
