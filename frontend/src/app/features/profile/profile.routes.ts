import { Routes } from '@angular/router';

export const PROFILE_ROUTES: Routes = [
  { path: '', title: 'Profile | Support CRM', loadComponent: () => import('./profile-page').then(m => m.ProfilePage) },
];
