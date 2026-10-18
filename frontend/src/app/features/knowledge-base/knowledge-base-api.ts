import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import { PagedResponse } from '../customers/customer-models';
import {
  KnowledgeArticle,
  KnowledgeArticleListParams,
  KnowledgeArticleSummary,
  KnowledgeCategory,
  SaveKnowledgeArticleRequest,
  TicketKnowledgeLink,
} from './knowledge-base-models';

@Injectable({ providedIn: 'root' })
export class KnowledgeBaseApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  listArticles({ page, pageSize, search, type, categoryId, status, mine }: KnowledgeArticleListParams): Observable<PagedResponse<KnowledgeArticleSummary>> {
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
    if (status) {
      params = params.set('status', status);
    }
    if (mine) {
      params = params.set('mine', true);
    }
    return this.http.get<PagedResponse<KnowledgeArticleSummary>>(this.apiUrl.versioned('knowledge-base/articles'), { params });
  }

  getArticle(id: string): Observable<KnowledgeArticle> {
    return this.http.get<KnowledgeArticle>(this.apiUrl.versioned(`knowledge-base/articles/${id}`));
  }

  createArticle(body: SaveKnowledgeArticleRequest): Observable<KnowledgeArticle> {
    return this.http.post<KnowledgeArticle>(this.apiUrl.versioned('knowledge-base/articles'), body);
  }

  updateArticle(id: string, body: SaveKnowledgeArticleRequest): Observable<KnowledgeArticle> {
    return this.http.put<KnowledgeArticle>(this.apiUrl.versioned(`knowledge-base/articles/${id}`), body);
  }

  submit(id: string): Observable<KnowledgeArticle> {
    return this.action(id, 'submit', {});
  }

  returnToDraft(id: string, reason: string | null): Observable<KnowledgeArticle> {
    return this.action(id, 'return', { reason });
  }

  publish(id: string): Observable<KnowledgeArticle> {
    return this.action(id, 'publish', {});
  }

  archive(id: string): Observable<KnowledgeArticle> {
    return this.action(id, 'archive', {});
  }

  restore(id: string): Observable<KnowledgeArticle> {
    return this.action(id, 'restore', {});
  }

  listCategories(includeInactive = false): Observable<KnowledgeCategory[]> {
    const params = new HttpParams().set('includeInactive', includeInactive);
    return this.http.get<KnowledgeCategory[]>(this.apiUrl.versioned('knowledge-base/categories'), { params });
  }

  createCategory(name: string): Observable<KnowledgeCategory> {
    return this.http.post<KnowledgeCategory>(this.apiUrl.versioned('knowledge-base/categories'), { name });
  }

  updateCategory(id: string, body: { name: string; isActive: boolean }): Observable<KnowledgeCategory> {
    return this.http.put<KnowledgeCategory>(this.apiUrl.versioned(`knowledge-base/categories/${id}`), body);
  }

  listTicketLinks(ticketId: string): Observable<TicketKnowledgeLink[]> {
    return this.http.get<TicketKnowledgeLink[]>(this.apiUrl.versioned(`tickets/${ticketId}/knowledge-articles`));
  }

  linkArticle(ticketId: string, articleId: string): Observable<TicketKnowledgeLink> {
    return this.http.post<TicketKnowledgeLink>(this.apiUrl.versioned(`tickets/${ticketId}/knowledge-articles`), { articleId });
  }

  unlinkArticle(ticketId: string, articleId: string): Observable<void> {
    return this.http.delete<void>(this.apiUrl.versioned(`tickets/${ticketId}/knowledge-articles/${articleId}`));
  }

  private action(id: string, name: string, body: object): Observable<KnowledgeArticle> {
    return this.http.post<KnowledgeArticle>(this.apiUrl.versioned(`knowledge-base/articles/${id}/${name}`), body);
  }
}
