import { LAZY_INTL_PROVIDERS } from '../../core/i18n/intl-providers';
import { Routes } from '@angular/router';
import { roleGuard } from '../../core/auth/auth-guards';

export const KNOWLEDGE_BASE_ROUTES: Routes = [
  { path: '', providers: LAZY_INTL_PROVIDERS, children: [
  { path: '', title: 'titles.knowledgeBase', loadComponent: () => import('./knowledge-base-page').then(m => m.KnowledgeBasePage) },
  { path: 'new', title: 'titles.newArticle', loadComponent: () => import('./knowledge-article-edit-page').then(m => m.KnowledgeArticleEditPage) },
  {
    path: 'categories',
    title: 'titles.knowledgeBaseCategories',
    canActivate: [roleGuard('SuperAdmin')],
    loadComponent: () => import('./knowledge-categories-page').then(m => m.KnowledgeCategoriesPage),
  },
  { path: ':id', title: 'titles.article', loadComponent: () => import('./knowledge-article-page').then(m => m.KnowledgeArticlePage) },
  { path: ':id/edit', title: 'titles.editArticle', loadComponent: () => import('./knowledge-article-edit-page').then(m => m.KnowledgeArticleEditPage) },
] },
];
