import { Routes } from '@angular/router';

export const COLLABORATION_ROUTES: Routes = [
  {
    path: '',
    title: 'My Collaborations | Support CRM',
    loadComponent: () => import('./my-collaborations-page').then(m => m.MyCollaborationsPage),
  },
  {
    path: 'tickets/:id',
    title: 'Ticket discussion | Support CRM',
    loadComponent: () => import('./collaboration-ticket-page').then(m => m.CollaborationTicketPage),
  },
];
