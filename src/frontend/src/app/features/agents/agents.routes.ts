import { Routes } from '@angular/router';
export const agentsRoutes:Routes=[{path:'',loadComponent:()=>import('./pages/agent-list.page').then(module=>module.AgentListPage)},{path:'new',loadComponent:()=>import('./pages/agent-create.page').then(module=>module.AgentCreatePage)},{path:':userId',loadComponent:()=>import('./pages/agent-edit.page').then(module=>module.AgentEditPage)}];
