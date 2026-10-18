import { Routes } from '@angular/router';

export const PROFILE_ROUTES: Routes = [
  { path: '', title: 'titles.profile', loadComponent: () => import('./profile-page').then(m => m.ProfilePage) },
];
