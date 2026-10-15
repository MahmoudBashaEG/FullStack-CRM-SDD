import { Injectable, inject } from '@angular/core';
import { API_CONFIG } from './api-config';

const trimLeadingSlashes = (path: string) => path.replace(/^\/+/, '');

/** The single place that builds backend URLs. Feature API services must use it. */
@Injectable({ providedIn: 'root' })
export class ApiUrl {
  private readonly config = inject(API_CONFIG);

  /** Business resources: versioned('tickets') -> '/api/v1/tickets'. */
  versioned(path: string): string {
    return `${this.config.baseUrl}/${this.config.version}/${trimLeadingSlashes(path)}`;
  }

  /** Infrastructure endpoints: unversioned('health') -> '/api/health'. */
  unversioned(path: string): string {
    return `${this.config.baseUrl}/${trimLeadingSlashes(path)}`;
  }
}
