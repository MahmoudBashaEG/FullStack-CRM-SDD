import { Routes } from '@angular/router';

export const SLA_ROUTES: Routes = [
  { path: '', title: 'SLA & Automation | Support CRM', loadComponent: () => import('./sla-admin-page').then(m => m.SlaAdminPage) },
];
