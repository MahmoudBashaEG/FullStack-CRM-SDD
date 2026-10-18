import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/auth/auth-guards';
import { Shell } from './core/layout/shell/shell';

export const routes: Routes = [
  {
    path: 'sign-in',
    title: 'Sign in | Support CRM',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/sign-in-page').then(m => m.SignInPage),
  },
  {
    path: 'sign-up',
    title: 'Create account | Support CRM',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/sign-up-page').then(m => m.SignUpPage),
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', loadChildren: () => import('./features/home/home.routes').then(m => m.HOME_ROUTES) },
      { path: 'profile', loadChildren: () => import('./features/profile/profile.routes').then(m => m.PROFILE_ROUTES) },
      {
        path: 'agent-dashboard',
        canActivate: [roleGuard('Agent')],
        loadChildren: () => import('./features/agent-dashboard/agent-dashboard.routes').then(m => m.AGENT_DASHBOARD_ROUTES),
      },
      {
        path: 'tickets',
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/tickets/tickets.routes').then(m => m.TICKETS_ROUTES),
      },
      {
        path: 'customers',
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/customers/customers.routes').then(m => m.CUSTOMERS_ROUTES),
      },
      {
        path: 'agents',
        canActivate: [roleGuard('SuperAdmin')],
        loadChildren: () => import('./features/agents/agents.routes').then(m => m.AGENTS_ROUTES),
      },
      {
        path: 'tasks',
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/tasks/tasks.routes').then(m => m.TASKS_ROUTES),
      },
      {
        path: 'notifications',
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/notifications/notifications.routes').then(m => m.NOTIFICATIONS_ROUTES),
      },
      {
        path: 'quick-replies',
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/quick-replies/quick-replies.routes').then(m => m.QUICK_REPLIES_ROUTES),
      },
      {
        path: 'collaboration',
        canActivate: [roleGuard('Agent')],
        loadChildren: () => import('./features/collaboration/collaboration.routes').then(m => m.COLLABORATION_ROUTES),
      },
      {
        path: 'sla',
        canActivate: [roleGuard('SuperAdmin')],
        loadChildren: () => import('./features/sla/sla.routes').then(m => m.SLA_ROUTES),
      },
      {
        path: 'knowledge-base',
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/knowledge-base/knowledge-base.routes').then(m => m.KNOWLEDGE_BASE_ROUTES),
      },
      {
        path: 'ai-assistant',
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/ai/ai.routes').then(m => m.AI_ROUTES),
      },
      {
        path: 'portal',
        canActivate: [roleGuard('Customer')],
        loadChildren: () => import('./features/portal/portal.routes').then(m => m.PORTAL_ROUTES),
      },
      {
        path: 'reports',
        canActivate: [roleGuard('SuperAdmin')],
        loadChildren: () => import('./features/reports/reports.routes').then(m => m.REPORTS_ROUTES),
      },
      // Feature stories add: { path: '<feature>', loadChildren: () => import('./features/<feature>/<feature>.routes') ... }
    ],
  },
  { path: '**', redirectTo: '' },
];
