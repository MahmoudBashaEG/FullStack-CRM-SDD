import { Routes } from '@angular/router';

export const NOTIFICATIONS_ROUTES: Routes = [
  {
    path: '',
    title: 'titles.notifications',
    loadComponent: () => import('./notifications-page').then(m => m.NotificationsPage),
  },
];
