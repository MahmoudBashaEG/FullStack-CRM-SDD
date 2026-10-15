import { InjectionToken, Provider } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface ApiConfig {
  /** API root without trailing slash, e.g. '/api'. */
  readonly baseUrl: string;
  /** Version segment for business routes, e.g. 'v1'. */
  readonly version: string;
}

export const API_CONFIG = new InjectionToken<ApiConfig>('API_CONFIG');

export function provideApiConfig(): Provider {
  return {
    provide: API_CONFIG,
    useValue: { baseUrl: environment.apiBaseUrl, version: environment.apiVersion } satisfies ApiConfig,
  };
}
