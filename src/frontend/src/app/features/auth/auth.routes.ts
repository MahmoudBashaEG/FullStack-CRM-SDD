import { Routes } from '@angular/router';
export const authRoutes:Routes=[{path:'signin',loadComponent:()=>import('./pages/signin.page').then(module=>module.SigninPage)},{path:'signup',loadComponent:()=>import('./pages/signup.page').then(module=>module.SignupPage)}];
