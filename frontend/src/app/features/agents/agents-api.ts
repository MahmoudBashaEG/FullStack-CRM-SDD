import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import { Agent, CreateAgentRequest, UpdateAgentRequest } from './agent-models';

@Injectable({ providedIn: 'root' })
export class AgentsApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  list(): Observable<Agent[]> {
    return this.http.get<Agent[]>(this.apiUrl.versioned('agents'));
  }

  get(id: string): Observable<Agent> {
    return this.http.get<Agent>(this.apiUrl.versioned(`agents/${id}`));
  }

  create(body: CreateAgentRequest): Observable<Agent> {
    return this.http.post<Agent>(this.apiUrl.versioned('agents'), body);
  }

  update(id: string, body: UpdateAgentRequest): Observable<Agent> {
    return this.http.put<Agent>(this.apiUrl.versioned(`agents/${id}`), body);
  }

  setStatus(id: string, isActive: boolean): Observable<void> {
    return this.http.put<void>(this.apiUrl.versioned(`agents/${id}/status`), { isActive });
  }

  setPassword(id: string, newPassword: string): Observable<void> {
    return this.http.put<void>(this.apiUrl.versioned(`agents/${id}/password`), { newPassword });
  }
}
