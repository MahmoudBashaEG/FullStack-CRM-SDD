import { Routes } from '@angular/router';

export const ADMIN_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'users' },
  { path: 'users', title: 'Users | Support CRM', loadComponent: () => import('./admin-users-page').then(m => m.AdminUsersPage) },
  { path: 'users/:id', title: 'Account | Support CRM', loadComponent: () => import('./admin-user-detail-page').then(m => m.AdminUserDetailPage) },
  { path: 'permissions', title: 'Roles & Permissions | Support CRM', loadComponent: () => import('./permissions-page').then(m => m.PermissionsPage) },
  { path: 'audit-logs', title: 'Audit Logs | Support CRM', loadComponent: () => import('./audit-logs-page').then(m => m.AuditLogsPage) },
  {
    path: 'settings',
    title: 'Organization Settings | Support CRM',
    loadComponent: () => import('./organization-settings-page').then(m => m.OrganizationSettingsPage),
  },
];
