import { LAZY_INTL_PROVIDERS } from '../../core/i18n/intl-providers';
import { Routes } from '@angular/router';

export const ADMIN_ROUTES: Routes = [
  { path: '', providers: LAZY_INTL_PROVIDERS, children: [
  { path: '', pathMatch: 'full', redirectTo: 'users' },
  { path: 'users', title: 'titles.users', loadComponent: () => import('./admin-users-page').then(m => m.AdminUsersPage) },
  { path: 'users/:id', title: 'titles.account', loadComponent: () => import('./admin-user-detail-page').then(m => m.AdminUserDetailPage) },
  { path: 'permissions', title: 'titles.rolesAndPermissions', loadComponent: () => import('./permissions-page').then(m => m.PermissionsPage) },
  { path: 'audit-logs', title: 'titles.auditLogs', loadComponent: () => import('./audit-logs-page').then(m => m.AuditLogsPage) },
  {
    path: 'settings',
    title: 'titles.organizationSettings',
    loadComponent: () => import('./organization-settings-page').then(m => m.OrganizationSettingsPage),
  },
] },
];
