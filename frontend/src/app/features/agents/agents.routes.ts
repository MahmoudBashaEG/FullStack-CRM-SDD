import { Routes } from '@angular/router';

export const AGENTS_ROUTES: Routes = [
  {
    path: '',
    title: 'Agents | Support CRM',
    loadComponent: () => import('./agent-list-page').then(m => m.AgentListPage),
  },
  {
    path: 'new',
    title: 'New agent | Support CRM',
    loadComponent: () => import('./agent-create-page').then(m => m.AgentCreatePage),
  },
  {
    path: ':id',
    title: 'Agent | Support CRM',
    loadComponent: () => import('./agent-detail-page').then(m => m.AgentDetailPage),
  },
];
