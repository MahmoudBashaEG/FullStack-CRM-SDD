import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResponse } from '../../features/customers/customer-models';
import { ApiUrl } from '../api/api-url';
import { SlaAlert, SlaAlertCount } from './notification-models';

/** Staff (SuperAdmin + Agent) SLA alerts. The server shows only the caller's alerts for tickets they can currently access. */
@Injectable({ providedIn: 'root' })
export class SlaAlertsApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  unreadCount(): Observable<SlaAlertCount> {
    return this.http.get<SlaAlertCount>(this.apiUrl.versioned('sla-alerts/unread-count'));
  }

  list(page: number, pageSize: number, unreadOnly: boolean): Observable<PagedResponse<SlaAlert>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize).set('unreadOnly', unreadOnly);
    return this.http.get<PagedResponse<SlaAlert>>(this.apiUrl.versioned('sla-alerts'), { params });
  }

  markRead(id: number): Observable<void> {
    return this.http.put<void>(this.apiUrl.versioned(`sla-alerts/${id}/read`), {});
  }
}
