import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import { PagedResponse } from '../customers/customer-models';
import {
  ChangeTicketAssigneeRequest,
  ChangeTicketCategoryRequest,
  CreateTicketRequest,
  EscalateTicketRequest,
  Ticket,
  TicketActivity,
  TicketListParams,
  TransitionTicketRequest,
  UpdateTicketRequest,
} from './ticket-models';

@Injectable({ providedIn: 'root' })
export class TicketsApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  /** Empty filters are omitted. The server additionally restricts Agents to their own tickets. */
  list({ page, pageSize, search, customerId, status, category, priority, escalated, slaBreached }: TicketListParams): Observable<PagedResponse<Ticket>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    const term = search?.trim();
    if (term) {
      params = params.set('search', term);
    }
    if (customerId) {
      params = params.set('customerId', customerId);
    }
    if (status) {
      params = params.set('status', status);
    }
    if (category) {
      params = params.set('category', category);
    }
    if (priority) {
      params = params.set('priority', priority);
    }
    if (escalated != null) {
      params = params.set('escalated', escalated);
    }
    if (slaBreached != null) {
      params = params.set('slaBreached', slaBreached);
    }
    return this.http.get<PagedResponse<Ticket>>(this.apiUrl.versioned('tickets'), { params });
  }

  get(id: string): Observable<Ticket> {
    return this.http.get<Ticket>(this.apiUrl.versioned(`tickets/${id}`));
  }

  create(body: CreateTicketRequest): Observable<Ticket> {
    return this.http.post<Ticket>(this.apiUrl.versioned('tickets'), body);
  }

  update(id: string, body: UpdateTicketRequest): Observable<Ticket> {
    return this.http.put<Ticket>(this.apiUrl.versioned(`tickets/${id}`), body);
  }

  /** Category only (e.g. applying an AI suggestion). 409 when the category changed since it was shown. */
  changeCategory(id: string, body: ChangeTicketCategoryRequest): Observable<Ticket> {
    return this.http.put<Ticket>(this.apiUrl.versioned(`tickets/${id}/category`), body);
  }

  changeAssignee(id: string, body: ChangeTicketAssigneeRequest): Observable<Ticket> {
    return this.http.put<Ticket>(this.apiUrl.versioned(`tickets/${id}/assignee`), body);
  }

  transition(id: string, body: TransitionTicketRequest): Observable<Ticket> {
    return this.http.post<Ticket>(this.apiUrl.versioned(`tickets/${id}/transitions`), body);
  }

  escalate(id: string, body: EscalateTicketRequest): Observable<Ticket> {
    return this.http.post<Ticket>(this.apiUrl.versioned(`tickets/${id}/escalation`), body);
  }

  /** Records the first customer response for SLA (server time, caller as actor). Sends nothing to the customer. */
  recordFirstResponse(id: string): Observable<Ticket> {
    return this.http.post<Ticket>(this.apiUrl.versioned(`tickets/${id}/first-response`), {});
  }

  clearEscalation(id: string): Observable<Ticket> {
    return this.http.delete<Ticket>(this.apiUrl.versioned(`tickets/${id}/escalation`));
  }

  /** Newest first; the server returns 404 when the ticket is not (or no longer) visible to the caller. */
  activity(id: string, page: number, pageSize: number): Observable<PagedResponse<TicketActivity>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResponse<TicketActivity>>(this.apiUrl.versioned(`tickets/${id}/activity`), { params });
  }
}
