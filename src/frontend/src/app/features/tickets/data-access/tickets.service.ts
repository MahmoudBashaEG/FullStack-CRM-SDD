import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { CreateTicket, Page, ReferenceChoice, TicketDetail, TicketHistory, TicketListParams, TicketSummary, TicketDetailsPatch, TicketStatus } from '../models/ticket.models';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class TicketsService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl.replace(/\/$/, '');
  create(input: CreateTicket) { return this.http.post<TicketDetail>(`${this.base}/tickets`, input); }
  update(reference: string, expectedVersion: string, patch: TicketDetailsPatch) { return this.http.patch<TicketDetail>(`${this.base}/tickets/${encodeURIComponent(reference)}/details`, { expectedVersion, ...patch }); }
  assign(reference: string, expectedVersion: string, agentId: string | null) { return this.http.put<TicketDetail>(`${this.base}/tickets/${encodeURIComponent(reference)}/assignment`, { expectedVersion, agentId }); }
  changeStatus(reference: string, expectedVersion: string, status: TicketStatus): Observable<TicketDetail> { return this.http.put<TicketDetail>(`${this.base}/tickets/${encodeURIComponent(reference)}/status`, { expectedVersion, status }); }
  list(criteria: TicketListParams = {}) {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(criteria)) if (value !== undefined) params = params.set(key, value);
    return this.http.get<Page<TicketSummary>>(`${this.base}/tickets`, { params });
  }
  detail(reference: string) { return this.http.get<TicketDetail>(`${this.base}/tickets/${encodeURIComponent(reference)}`); }
  history(reference: string, page = 1) {
    return this.http.get<Page<TicketHistory>>(`${this.base}/tickets/${encodeURIComponent(reference)}/history`, { params: new HttpParams().set('page', page).set('pageSize', 25) });
  }
  categories() { return this.http.get<ReferenceChoice[]>(`${this.base}/ticket-reference-data/categories`); }
  agents() { return this.http.get<ReferenceChoice[]>(`${this.base}/ticket-reference-data/agents`); }
}
