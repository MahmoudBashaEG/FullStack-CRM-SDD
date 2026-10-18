import { LAZY_INTL_PROVIDERS } from '../../core/i18n/intl-providers';
import { Routes } from '@angular/router';

export const CUSTOMERS_ROUTES: Routes = [
  { path: '', providers: LAZY_INTL_PROVIDERS, children: [
  {
    path: '',
    title: 'titles.customers',
    loadComponent: () => import('./customer-list-page').then(m => m.CustomerListPage),
  },
  {
    path: 'new',
    title: 'titles.newCustomer',
    loadComponent: () => import('./customer-create-page').then(m => m.CustomerCreatePage),
  },
  {
    path: ':id',
    title: 'titles.customer',
    loadComponent: () => import('./customer-detail-page').then(m => m.CustomerDetailPage),
  },
] },
];
