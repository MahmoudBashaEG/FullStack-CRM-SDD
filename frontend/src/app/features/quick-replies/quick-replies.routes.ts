import { Routes } from '@angular/router';

export const QUICK_REPLIES_ROUTES: Routes = [
  { path: '', title: 'Quick Replies | Support CRM', loadComponent: () => import('./quick-reply-list-page').then(m => m.QuickReplyListPage) },
  { path: 'new', title: 'New quick reply | Support CRM', loadComponent: () => import('./quick-reply-edit-page').then(m => m.QuickReplyEditPage) },
  { path: ':id', title: 'Quick reply | Support CRM', loadComponent: () => import('./quick-reply-edit-page').then(m => m.QuickReplyEditPage) },
];
