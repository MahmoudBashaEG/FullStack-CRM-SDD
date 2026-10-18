import { Routes } from '@angular/router';

export const AGENTS_ROUTES: Routes = [
  {
    path: '',
    title: 'titles.agents',
    loadComponent: () => import('./agent-list-page').then(m => m.AgentListPage),
  },
  {
    path: 'new',
    title: 'titles.newAgent',
    loadComponent: () => import('./agent-create-page').then(m => m.AgentCreatePage),
  },
  {
    path: ':id',
    title: 'titles.agent',
    loadComponent: () => import('./agent-detail-page').then(m => m.AgentDetailPage),
  },
];
