import { LAZY_INTL_PROVIDERS } from '../../core/i18n/intl-providers';
import { Routes } from '@angular/router';

export const TASKS_ROUTES: Routes = [
  { path: '', providers: LAZY_INTL_PROVIDERS, children: [
  { path: '', title: 'titles.tasks', loadComponent: () => import('./task-list-page').then(m => m.TaskListPage) },
  { path: 'new', title: 'titles.newTask', loadComponent: () => import('./task-edit-page').then(m => m.TaskEditPage) },
  { path: ':id', title: 'titles.task', loadComponent: () => import('./task-edit-page').then(m => m.TaskEditPage) },
] },
];
