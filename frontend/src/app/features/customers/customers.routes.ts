import { Routes } from '@angular/router';

export const CUSTOMERS_ROUTES: Routes = [
  {
    path: '',
    title: 'Customers | Support CRM',
    loadComponent: () => import('./customer-list-page').then(m => m.CustomerListPage),
  },
  {
    path: 'new',
    title: 'New customer | Support CRM',
    loadComponent: () => import('./customer-create-page').then(m => m.CustomerCreatePage),
  },
  {
    path: ':id',
    title: 'Customer | Support CRM',
    loadComponent: () => import('./customer-detail-page').then(m => m.CustomerDetailPage),
  },
];
