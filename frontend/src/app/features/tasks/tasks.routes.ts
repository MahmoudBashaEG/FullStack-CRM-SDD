import { Routes } from '@angular/router';

export const TASKS_ROUTES: Routes = [
  { path: '', title: 'Tasks | Support CRM', loadComponent: () => import('./task-list-page').then(m => m.TaskListPage) },
  { path: 'new', title: 'New task | Support CRM', loadComponent: () => import('./task-edit-page').then(m => m.TaskEditPage) },
  { path: ':id', title: 'Task | Support CRM', loadComponent: () => import('./task-edit-page').then(m => m.TaskEditPage) },
];
