import { Routes } from '@angular/router';

export const REPORTS_ROUTES: Routes = [
  {
    path: '',
    title: 'Management Dashboard | Support CRM',
    loadComponent: () => import('./management-dashboard-page').then((m) => m.ManagementDashboardPage),
  },
  {
    path: 'tickets',
    title: 'Ticket Reports | Support CRM',
    loadComponent: () => import('./ticket-report-page').then((m) => m.TicketReportPage),
  },
  {
    path: 'sla',
    title: 'SLA Performance | Support CRM',
    loadComponent: () => import('./sla-report-page').then((m) => m.SlaReportPage),
  },
  {
    path: 'agents',
    title: 'Agent Performance | Support CRM',
    loadComponent: () => import('./agent-report-page').then((m) => m.AgentReportPage),
  },
  {
    path: 'customer-satisfaction',
    title: 'Customer Satisfaction | Support CRM',
    loadComponent: () => import('./csat-report-page').then((m) => m.CsatReportPage),
  },
];
