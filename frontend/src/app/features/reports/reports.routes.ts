import { LAZY_INTL_PROVIDERS } from '../../core/i18n/intl-providers';
import { Routes } from '@angular/router';

export const REPORTS_ROUTES: Routes = [
  { path: '', providers: LAZY_INTL_PROVIDERS, children: [
  {
    path: '',
    title: 'titles.managementDashboard',
    loadComponent: () => import('./management-dashboard-page').then((m) => m.ManagementDashboardPage),
  },
  {
    path: 'tickets',
    title: 'titles.ticketReports',
    loadComponent: () => import('./ticket-report-page').then((m) => m.TicketReportPage),
  },
  {
    path: 'sla',
    title: 'titles.slaPerformance',
    loadComponent: () => import('./sla-report-page').then((m) => m.SlaReportPage),
  },
  {
    path: 'agents',
    title: 'titles.agentPerformance',
    loadComponent: () => import('./agent-report-page').then((m) => m.AgentReportPage),
  },
  {
    path: 'customer-satisfaction',
    title: 'titles.customerSatisfaction',
    loadComponent: () => import('./csat-report-page').then((m) => m.CsatReportPage),
  },
] },
];
