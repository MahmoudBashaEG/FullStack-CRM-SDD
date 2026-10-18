import { Routes } from '@angular/router';

export const HOME_ROUTES: Routes = [
  { path: '', title: 'titles.home', loadComponent: () => import('./home-page').then(m => m.HomePage) },
];
