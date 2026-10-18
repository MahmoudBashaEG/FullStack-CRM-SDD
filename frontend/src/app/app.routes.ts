import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/auth/auth-guards';
import { Shell } from './core/layout/shell/shell';

export const routes: Routes = [
  {
    path: 'sign-in',
    title: 'titles.signIn',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/sign-in-page').then(m => m.SignInPage),
  },
  {
    path: 'sign-up',
    title: 'titles.createAccount',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/sign-up-page').then(m => m.SignUpPage),
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', loadChildren: () => import('./features/home/home.routes').then(m => m.HOME_ROUTES) },
      { path: 'profile', data: { crumb: 'nav.profile' }, loadChildren: () => import('./features/profile/profile.routes').then(m => m.PROFILE_ROUTES) },
      {
        path: 'agent-dashboard', data: { crumb: 'nav.myDashboard' },
        canActivate: [roleGuard('Agent')],
        loadChildren: () => import('./features/agent-dashboard/agent-dashboard.routes').then(m => m.AGENT_DASHBOARD_ROUTES),
      },
      {
        path: 'tickets', data: { crumb: 'nav.tickets' },
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/tickets/tickets.routes').then(m => m.TICKETS_ROUTES),
      },
      {
        path: 'customers', data: { crumb: 'nav.customers' },
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/customers/customers.routes').then(m => m.CUSTOMERS_ROUTES),
      },
      {
        path: 'agents', data: { crumb: 'nav.agents' },
        canActivate: [roleGuard('SuperAdmin')],
        loadChildren: () => import('./features/agents/agents.routes').then(m => m.AGENTS_ROUTES),
      },
      {
        path: 'tasks', data: { crumb: 'nav.tasks' },
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/tasks/tasks.routes').then(m => m.TASKS_ROUTES),
      },
      {
        path: 'notifications', data: { crumb: 'nav.notifications' },
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/notifications/notifications.routes').then(m => m.NOTIFICATIONS_ROUTES),
      },
      {
        path: 'quick-replies', data: { crumb: 'nav.quickReplies' },
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/quick-replies/quick-replies.routes').then(m => m.QUICK_REPLIES_ROUTES),
      },
      {
        path: 'collaboration', data: { crumb: 'nav.myCollaborations' },
        canActivate: [roleGuard('Agent')],
        loadChildren: () => import('./features/collaboration/collaboration.routes').then(m => m.COLLABORATION_ROUTES),
      },
      {
        path: 'sla', data: { crumb: 'nav.slaAndAutomation' },
        canActivate: [roleGuard('SuperAdmin')],
        loadChildren: () => import('./features/sla/sla.routes').then(m => m.SLA_ROUTES),
      },
      {
        path: 'knowledge-base', data: { crumb: 'nav.knowledgeBase' },
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/knowledge-base/knowledge-base.routes').then(m => m.KNOWLEDGE_BASE_ROUTES),
      },
      {
        path: 'ai-assistant', data: { crumb: 'nav.aiAssistant' },
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/ai/ai.routes').then(m => m.AI_ROUTES),
      },
      {
        path: 'portal', data: { crumb: 'nav.supportPortal' },
        canActivate: [roleGuard('Customer')],
        loadChildren: () => import('./features/portal/portal.routes').then(m => m.PORTAL_ROUTES),
      },
      {
        path: 'reports', data: { crumb: 'nav.reports' },
        canActivate: [roleGuard('SuperAdmin')],
        loadChildren: () => import('./features/reports/reports.routes').then(m => m.REPORTS_ROUTES),
      },
      {
        path: 'admin', data: { crumb: 'nav.administration' },
        canActivate: [roleGuard('SuperAdmin')],
        loadChildren: () => import('./features/admin/admin.routes').then(m => m.ADMIN_ROUTES),
      },
      // Feature stories add: { path: '<feature>', loadChildren: () => import('./features/<feature>/<feature>.routes') ... }
    ],
  },
  { path: '**', redirectTo: '' },
];
