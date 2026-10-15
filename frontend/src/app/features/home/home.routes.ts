import { Routes } from '@angular/router';

export const HOME_ROUTES: Routes = [
  { path: '', title: 'Home | Support CRM', loadComponent: () => import('./home-page').then(m => m.HomePage) },
];
