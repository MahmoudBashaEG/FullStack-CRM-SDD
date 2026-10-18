import { provideHttpClient, withFetch, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { TitleStrategy, provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { provideApiConfig } from './core/api/api-config';
import { authInterceptor } from './core/auth/auth-interceptor';
import { AuthService } from './core/auth/auth-service';
import { Branding } from './core/branding/branding';
import { I18n } from './core/i18n/i18n';
import { CrmTitleStrategy } from './core/layout/crm-title-strategy';
import { Theme } from './core/preferences/theme';
import { provideCrmDateAdapter } from './shared/date/crm-date-adapter';

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
    provideCrmDateAdapter(),
    { provide: TitleStrategy, useClass: CrmTitleStrategy },
    // Order matters: language first (no English flash), then branding (public, never rejects), then the session.
    provideAppInitializer(() => inject(I18n).init()),
    provideAppInitializer(() => inject(Theme).init()),
    provideAppInitializer(() => inject(Branding).load()),
    provideAppInitializer(() => inject(AuthService).restoreSession()),
  ]
};
