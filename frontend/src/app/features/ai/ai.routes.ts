import { Routes } from '@angular/router';

export const AI_ROUTES: Routes = [
  { path: '', title: 'AI Assistant | Support CRM', loadComponent: () => import('./ai-assistant-page').then(m => m.AiAssistantPage) },
];
