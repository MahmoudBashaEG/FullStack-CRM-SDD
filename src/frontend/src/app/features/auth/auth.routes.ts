import { Routes } from '@angular/router';
export const authRoutes:Routes=[{path:'signin',loadComponent:()=>import('./pages/signin.page').then(module=>module.SigninPage)}];
