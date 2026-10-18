import { LAZY_INTL_PROVIDERS } from '../../core/i18n/intl-providers';
import { Routes } from '@angular/router';

export const TICKETS_ROUTES: Routes = [
  { path: '', providers: LAZY_INTL_PROVIDERS, children: [
  {
    path: '',
    title: 'titles.tickets',
    loadComponent: () => import('./ticket-list-page').then(m => m.TicketListPage),
  },
  {
    path: 'new',
    title: 'titles.newTicket',
    loadComponent: () => import('./ticket-create-page').then(m => m.TicketCreatePage),
  },
  {
    path: ':id',
    title: 'titles.ticket',
    loadComponent: () => import('./ticket-detail-page').then(m => m.TicketDetailPage),
  },
] },
];
