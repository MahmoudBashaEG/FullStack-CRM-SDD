import { Routes } from '@angular/router';

export const NOTIFICATIONS_ROUTES: Routes = [
  {
    path: '',
    title: 'Notifications | Support CRM',
    loadComponent: () => import('./notifications-page').then(m => m.NotificationsPage),
  },
];
