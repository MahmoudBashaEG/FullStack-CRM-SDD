import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { CreateTicket, Page, ReferenceChoice, TicketDetail, TicketHistory } from '../models/ticket.models';

@Injectable({ providedIn: 'root' })
export class TicketsService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl.replace(/\/$/, '');
  create(input: CreateTicket) { return this.http.post<TicketDetail>(`${this.base}/tickets`, input); }
  detail(reference: string) { return this.http.get<TicketDetail>(`${this.base}/tickets/${encodeURIComponent(reference)}`); }
  history(reference: string, page = 1) {
    return this.http.get<Page<TicketHistory>>(`${this.base}/tickets/${encodeURIComponent(reference)}/history`, { params: new HttpParams().set('page', page).set('pageSize', 25) });
  }
  categories() { return this.http.get<ReferenceChoice[]>(`${this.base}/ticket-reference-data/categories`); }
  agents() { return this.http.get<ReferenceChoice[]>(`${this.base}/ticket-reference-data/agents`); }
}
