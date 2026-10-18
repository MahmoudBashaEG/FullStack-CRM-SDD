import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResponse } from '../../features/customers/customer-models';
import { ApiUrl } from '../api/api-url';
import { TaskReminder, TaskReminderCheck } from './notification-models';

@Injectable({ providedIn: 'root' })
export class NotificationsApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  /** Issues any due reminders for the caller (idempotent on the server) and returns the unread count. */
  check(): Observable<TaskReminderCheck> {
    return this.http.post<TaskReminderCheck>(this.apiUrl.versioned('notifications/check'), {});
  }

  list(page: number, pageSize: number, unreadOnly: boolean): Observable<PagedResponse<TaskReminder>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize).set('unreadOnly', unreadOnly);
    return this.http.get<PagedResponse<TaskReminder>>(this.apiUrl.versioned('notifications'), { params });
  }

  markRead(id: number): Observable<void> {
    return this.http.put<void>(this.apiUrl.versioned(`notifications/${id}/read`), {});
  }
}
