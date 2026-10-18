import { Routes } from '@angular/router';

export const AI_ROUTES: Routes = [
  { path: '', title: 'titles.aiAssistant', loadComponent: () => import('./ai-assistant-page').then(m => m.AiAssistantPage) },
];
