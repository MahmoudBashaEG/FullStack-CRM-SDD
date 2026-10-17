import { Routes } from '@angular/router';

export const AGENT_DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    title: 'My Dashboard | Support CRM',
    loadComponent: () => import('./agent-dashboard-page').then(m => m.AgentDashboardPage),
  },
];
