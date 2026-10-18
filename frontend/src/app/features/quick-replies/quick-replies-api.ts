import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import { PagedResponse } from '../customers/customer-models';
import { QuickReply, QuickReplyListParams, SaveQuickReplyRequest } from './quick-reply-models';

@Injectable({ providedIn: 'root' })
export class QuickRepliesApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  list({ page, pageSize, search, category, scope, archived }: QuickReplyListParams): Observable<PagedResponse<QuickReply>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize).set('archived', archived);
    const term = search?.trim();
    if (term) {
      params = params.set('search', term);
    }
    if (category) {
      params = params.set('category', category);
    }
    if (scope) {
      params = params.set('scope', scope);
    }
    return this.http.get<PagedResponse<QuickReply>>(this.apiUrl.versioned('quick-replies'), { params });
  }

  get(id: string): Observable<QuickReply> {
    return this.http.get<QuickReply>(this.apiUrl.versioned(`quick-replies/${id}`));
  }

  create(body: SaveQuickReplyRequest): Observable<QuickReply> {
    return this.http.post<QuickReply>(this.apiUrl.versioned('quick-replies'), body);
  }

  update(id: string, body: SaveQuickReplyRequest): Observable<QuickReply> {
    return this.http.put<QuickReply>(this.apiUrl.versioned(`quick-replies/${id}`), body);
  }

  archive(id: string): Observable<QuickReply> {
    return this.http.post<QuickReply>(this.apiUrl.versioned(`quick-replies/${id}/archive`), {});
  }

  restore(id: string): Observable<QuickReply> {
    return this.http.delete<QuickReply>(this.apiUrl.versioned(`quick-replies/${id}/archive`));
  }
}
