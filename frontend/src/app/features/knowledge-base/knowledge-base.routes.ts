import { Routes } from '@angular/router';
import { roleGuard } from '../../core/auth/auth-guards';

export const KNOWLEDGE_BASE_ROUTES: Routes = [
  { path: '', title: 'Knowledge Base | Support CRM', loadComponent: () => import('./knowledge-base-page').then(m => m.KnowledgeBasePage) },
  { path: 'new', title: 'New article | Support CRM', loadComponent: () => import('./knowledge-article-edit-page').then(m => m.KnowledgeArticleEditPage) },
  {
    path: 'categories',
    title: 'Knowledge Base categories | Support CRM',
    canActivate: [roleGuard('SuperAdmin')],
    loadComponent: () => import('./knowledge-categories-page').then(m => m.KnowledgeCategoriesPage),
  },
  { path: ':id', title: 'Article | Support CRM', loadComponent: () => import('./knowledge-article-page').then(m => m.KnowledgeArticlePage) },
  { path: ':id/edit', title: 'Edit article | Support CRM', loadComponent: () => import('./knowledge-article-edit-page').then(m => m.KnowledgeArticleEditPage) },
];
