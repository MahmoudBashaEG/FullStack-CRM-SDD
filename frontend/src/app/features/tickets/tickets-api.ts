import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import { PagedResponse } from '../customers/customer-models';
import { CreateTicketRequest, Ticket, TicketListParams, UpdateTicketRequest } from './ticket-models';

@Injectable({ providedIn: 'root' })
export class TicketsApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  /** Empty filters are omitted. The server additionally restricts Agents to their own tickets. */
  list({ page, pageSize, search, customerId, status, category, priority }: TicketListParams): Observable<PagedResponse<Ticket>> {
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
}
