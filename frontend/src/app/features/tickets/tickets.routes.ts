import { Routes } from '@angular/router';

export const TICKETS_ROUTES: Routes = [
  {
    path: '',
    title: 'Tickets | Support CRM',
    loadComponent: () => import('./ticket-list-page').then(m => m.TicketListPage),
  },
  {
    path: 'new',
    title: 'New ticket | Support CRM',
    loadComponent: () => import('./ticket-create-page').then(m => m.TicketCreatePage),
  },
  {
    path: ':id',
    title: 'Ticket | Support CRM',
    loadComponent: () => import('./ticket-detail-page').then(m => m.TicketDetailPage),
  },
];
