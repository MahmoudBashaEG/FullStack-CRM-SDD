import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import { AgentDashboard } from './agent-dashboard-models';

@Injectable({ providedIn: 'root' })
export class AgentDashboardApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  /** No parameters: the server derives the Agent from the access token and scopes every number to them. */
  get(): Observable<AgentDashboard> {
    return this.http.get<AgentDashboard>(this.apiUrl.versioned('agent-dashboard'));
  }
}
