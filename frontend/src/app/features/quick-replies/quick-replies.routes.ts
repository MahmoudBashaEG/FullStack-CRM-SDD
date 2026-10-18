import { LAZY_INTL_PROVIDERS } from '../../core/i18n/intl-providers';
import { Routes } from '@angular/router';

export const QUICK_REPLIES_ROUTES: Routes = [
  { path: '', providers: LAZY_INTL_PROVIDERS, children: [
  { path: '', title: 'titles.quickReplies', loadComponent: () => import('./quick-reply-list-page').then(m => m.QuickReplyListPage) },
  { path: 'new', title: 'titles.newQuickReply', loadComponent: () => import('./quick-reply-edit-page').then(m => m.QuickReplyEditPage) },
  { path: ':id', title: 'titles.quickReply', loadComponent: () => import('./quick-reply-edit-page').then(m => m.QuickReplyEditPage) },
] },
];
