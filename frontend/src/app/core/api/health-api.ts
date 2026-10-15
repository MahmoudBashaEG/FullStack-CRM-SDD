import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from './api-url';

/** Transport contract for GET /api/health (matches Crm.Api HealthResponse). */
export interface HealthResponse {
  status: string;
  service: string;
  checkedAtUtc: string;
}

@Injectable({ providedIn: 'root' })
export class HealthApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  getHealth(): Observable<HealthResponse> {
    return this.http.get<HealthResponse>(this.apiUrl.unversioned('health'));
  }
}
