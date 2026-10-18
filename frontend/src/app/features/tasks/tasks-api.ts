import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import { PagedResponse } from '../customers/customer-models';
import {
  AgentTask,
  AgentTaskSummary,
  CreateTaskRequest,
  LinkableTicket,
  TaskListParams,
  UpdateTaskRequest,
} from './task-models';

/** Every mutation sends the task version the caller last saw as If-Match (F1). Stale ⇒ 409, missing ⇒ 428. */
@Injectable({ providedIn: 'root' })
export class TasksApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  /** Empty filters are omitted. The server derives ownership from the token. */
  list({ page, pageSize, search, status, dueState, archived, hasReminder, assignedAgentUserId }: TaskListParams): Observable<PagedResponse<AgentTask>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize).set('archived', archived);
    const term = search?.trim();
    if (term) {
      params = params.set('search', term);
    }
    if (status) {
      params = params.set('status', status);
    }
    if (dueState) {
      params = params.set('dueState', dueState);
    }
    if (hasReminder != null) {
      params = params.set('hasReminder', hasReminder);
    }
    if (assignedAgentUserId) {
      params = params.set('assignedAgentUserId', assignedAgentUserId);
    }
    return this.http.get<PagedResponse<AgentTask>>(this.apiUrl.versioned('tasks'), { params });
  }

  get(id: string): Observable<AgentTask> {
    return this.http.get<AgentTask>(this.apiUrl.versioned(`tasks/${id}`));
  }

  create(body: CreateTaskRequest): Observable<AgentTask> {
    return this.http.post<AgentTask>(this.apiUrl.versioned('tasks'), body);
  }

  linkableTickets(ownerUserId: string | null, search: string): Observable<LinkableTicket[]> {
    let params = new HttpParams();
    if (ownerUserId) {
      params = params.set('ownerUserId', ownerUserId);
    }
    const term = search.trim();
    if (term) {
      params = params.set('search', term);
    }
    return this.http.get<LinkableTicket[]>(this.apiUrl.versioned('tasks/linkable-tickets'), { params });
  }

  summary(): Observable<AgentTaskSummary> {
    return this.http.get<AgentTaskSummary>(this.apiUrl.versioned('agent-dashboard/tasks'));
  }

  update(id: string, version: string, body: UpdateTaskRequest): Observable<AgentTask> {
    return this.http.put<AgentTask>(this.apiUrl.versioned(`tasks/${id}`), body, this.ifMatch(version));
  }

  setTicketLink(id: string, version: string, ticketId: string): Observable<AgentTask> {
    return this.http.put<AgentTask>(this.apiUrl.versioned(`tasks/${id}/ticket-link`), { ticketId }, this.ifMatch(version));
  }

  removeTicketLink(id: string, version: string): Observable<AgentTask> {
    return this.http.delete<AgentTask>(this.apiUrl.versioned(`tasks/${id}/ticket-link`), this.ifMatch(version));
  }

  scheduleReminder(id: string, version: string, remindAtUtc: string): Observable<AgentTask> {
    return this.http.put<AgentTask>(this.apiUrl.versioned(`tasks/${id}/reminder`), { remindAtUtc }, this.ifMatch(version));
  }

  cancelReminder(id: string, version: string): Observable<AgentTask> {
    return this.http.delete<AgentTask>(this.apiUrl.versioned(`tasks/${id}/reminder`), this.ifMatch(version));
  }

  complete(id: string, version: string): Observable<AgentTask> {
    return this.http.post<AgentTask>(this.apiUrl.versioned(`tasks/${id}/completion`), {}, this.ifMatch(version));
  }

  reopen(id: string, version: string): Observable<AgentTask> {
    return this.http.delete<AgentTask>(this.apiUrl.versioned(`tasks/${id}/completion`), this.ifMatch(version));
  }

  archive(id: string, version: string): Observable<AgentTask> {
    return this.http.post<AgentTask>(this.apiUrl.versioned(`tasks/${id}/archive`), {}, this.ifMatch(version));
  }

  restore(id: string, version: string): Observable<AgentTask> {
    return this.http.delete<AgentTask>(this.apiUrl.versioned(`tasks/${id}/archive`), this.ifMatch(version));
  }

  reassign(id: string, version: string, assignedAgentUserId: string, removeTicketLink: boolean): Observable<AgentTask> {
    return this.http.put<AgentTask>(
      this.apiUrl.versioned(`tasks/${id}/assignee`),
      { assignedAgentUserId, removeTicketLink },
      this.ifMatch(version),
    );
  }

  private ifMatch(version: string): { headers: { 'If-Match': string } } {
    return { headers: { 'If-Match': `"${version}"` } };
  }
}
