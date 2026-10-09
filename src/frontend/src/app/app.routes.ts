import { Routes } from '@angular/router';
import { supportQueueGuard } from './core/auth/support-queue.guard';
export const routes:Routes=[
  {path:'auth',loadChildren:()=>import('./features/auth/auth.routes').then(module=>module.authRoutes)},
  {path:'tickets',canActivate:[supportQueueGuard],canActivateChild:[supportQueueGuard],loadChildren:()=>import('./features/tickets/tickets.routes').then(module=>module.ticketsRoutes)},
  {path:'access',loadComponent:()=>import('./core/auth/access.page').then(module=>module.AccessPage)},
  {path:'',pathMatch:'full',redirectTo:'access'},
  {path:'**',redirectTo:'access'}
];
