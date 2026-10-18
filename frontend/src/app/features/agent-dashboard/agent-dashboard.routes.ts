import { Routes } from '@angular/router';

export const AGENT_DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    title: 'titles.myDashboard',
    loadComponent: () => import('./agent-dashboard-page').then(m => m.AgentDashboardPage),
  },
];
