import { provideHttpClient, withFetch, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { provideApiConfig } from './core/api/api-config';
import { authInterceptor } from './core/auth/auth-interceptor';
import { AuthService } from './core/auth/auth-service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(
      withFetch(),
      withInterceptors([authInterceptor]),
      // Must match Crm.Api AuthCookies (double-submit CSRF for refresh/logout).
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
    ),
    provideApiConfig(),
    provideAppInitializer(() => inject(AuthService).restoreSession()),
  ]
};
