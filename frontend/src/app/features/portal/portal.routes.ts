import { Routes } from '@angular/router';

export const PORTAL_ROUTES: Routes = [
  { path: '', title: 'Support Portal | Support CRM', loadComponent: () => import('./portal-home-page').then(m => m.PortalHomePage) },
  { path: 'tickets', title: 'My tickets | Support CRM', loadComponent: () => import('./portal-ticket-list-page').then(m => m.PortalTicketListPage) },
  { path: 'new-ticket', title: 'New support request | Support CRM', loadComponent: () => import('./portal-ticket-create-page').then(m => m.PortalTicketCreatePage) },
  { path: 'tickets/:id', title: 'My request | Support CRM', loadComponent: () => import('./portal-ticket-detail-page').then(m => m.PortalTicketDetailPage) },
  { path: 'help-center', title: 'Help Center | Support CRM', loadComponent: () => import('./help-center-page').then(m => m.HelpCenterPage) },
  { path: 'help-center/:id', title: 'Help article | Support CRM', loadComponent: () => import('./help-center-article-page').then(m => m.HelpCenterArticlePage) },
];
