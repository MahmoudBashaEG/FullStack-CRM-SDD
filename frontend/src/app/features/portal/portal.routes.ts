import { LAZY_INTL_PROVIDERS } from '../../core/i18n/intl-providers';
import { Routes } from '@angular/router';

export const PORTAL_ROUTES: Routes = [
  { path: '', providers: LAZY_INTL_PROVIDERS, children: [
  { path: '', title: 'titles.supportPortal', loadComponent: () => import('./portal-home-page').then(m => m.PortalHomePage) },
  { path: 'tickets', title: 'titles.myTickets', loadComponent: () => import('./portal-ticket-list-page').then(m => m.PortalTicketListPage) },
  { path: 'new-ticket', title: 'titles.newSupportRequest', loadComponent: () => import('./portal-ticket-create-page').then(m => m.PortalTicketCreatePage) },
  { path: 'tickets/:id', title: 'titles.myRequest', loadComponent: () => import('./portal-ticket-detail-page').then(m => m.PortalTicketDetailPage) },
  { path: 'help-center', title: 'titles.helpCenter', loadComponent: () => import('./help-center-page').then(m => m.HelpCenterPage) },
  { path: 'help-center/:id', title: 'titles.helpArticle', loadComponent: () => import('./help-center-article-page').then(m => m.HelpCenterArticlePage) },
] },
];
