import { LAZY_INTL_PROVIDERS } from '../../core/i18n/intl-providers';
import { Routes } from '@angular/router';

export const COLLABORATION_ROUTES: Routes = [
  { path: '', providers: LAZY_INTL_PROVIDERS, children: [
  {
    path: '',
    title: 'titles.myCollaborations',
    loadComponent: () => import('./my-collaborations-page').then(m => m.MyCollaborationsPage),
  },
  {
    path: 'tickets/:id',
    title: 'titles.ticketDiscussion',
    loadComponent: () => import('./collaboration-ticket-page').then(m => m.CollaborationTicketPage),
  },
] },
];
