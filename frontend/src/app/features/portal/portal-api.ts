import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import { PagedResponse } from '../customers/customer-models';
import {
  HelpCenterArticle,
  HelpCenterArticleSummary,
  HelpCenterCategory,
  HelpCenterListParams,
  PortalTicket,
  PortalTicketListParams,
  PortalTicketSummary,
  PortalTimelineEntry,
  SavePortalFeedbackRequest,
  SubmitPortalTicketRequest,
} from './portal-models';

/** Customer Portal endpoints (/api/v1/portal/**). Customer role only; the server derives the customer from the token. */
@Injectable({ providedIn: 'root' })
export class PortalApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  /** Empty filters are omitted. */
  listTickets({ page, pageSize, search, status }: PortalTicketListParams): Observable<PagedResponse<PortalTicketSummary>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    const term = search?.trim();
    if (term) {
      params = params.set('search', term);
    }
    if (status) {
      params = params.set('status', status);
    }
    return this.http.get<PagedResponse<PortalTicketSummary>>(this.apiUrl.versioned('portal/tickets'), { params });
  }

  getTicket(id: string): Observable<PortalTicket> {
    return this.http.get<PortalTicket>(this.apiUrl.versioned(`portal/tickets/${id}`));
  }

  submitTicket(body: SubmitPortalTicketRequest): Observable<PortalTicket> {
    return this.http.post<PortalTicket>(this.apiUrl.versioned('portal/tickets'), body);
  }

  timeline(id: string): Observable<PortalTimelineEntry[]> {
    return this.http.get<PortalTimelineEntry[]>(this.apiUrl.versioned(`portal/tickets/${id}/timeline`));
  }

  saveFeedback(id: string, body: SavePortalFeedbackRequest): Observable<PortalTicket> {
    return this.http.put<PortalTicket>(this.apiUrl.versioned(`portal/tickets/${id}/feedback`), body);
  }

  listArticles({ page, pageSize, search, type, categoryId }: HelpCenterListParams): Observable<PagedResponse<HelpCenterArticleSummary>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    const term = search?.trim();
    if (term) {
      params = params.set('search', term);
    }
    if (type) {
      params = params.set('type', type);
    }
    if (categoryId) {
      params = params.set('categoryId', categoryId);
    }
    return this.http.get<PagedResponse<HelpCenterArticleSummary>>(this.apiUrl.versioned('portal/help-center/articles'), { params });
  }

  getArticle(id: string): Observable<HelpCenterArticle> {
    return this.http.get<HelpCenterArticle>(this.apiUrl.versioned(`portal/help-center/articles/${id}`));
  }

  categories(): Observable<HelpCenterCategory[]> {
    return this.http.get<HelpCenterCategory[]>(this.apiUrl.versioned('portal/help-center/categories'));
  }
}
