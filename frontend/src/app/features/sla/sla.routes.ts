import { LAZY_INTL_PROVIDERS } from '../../core/i18n/intl-providers';
import { Routes } from '@angular/router';

export const SLA_ROUTES: Routes = [
  { path: '', providers: LAZY_INTL_PROVIDERS, children: [
  { path: '', title: 'titles.slaAndAutomation', loadComponent: () => import('./sla-admin-page').then(m => m.SlaAdminPage) },
] },
];
