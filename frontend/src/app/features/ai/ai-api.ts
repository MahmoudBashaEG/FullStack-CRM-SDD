import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import { AiCategorySuggestion, AiChatRequest, AiChatResponse, AiReplyTone, AiSolutions, AiText } from './ai-models';

/** AI-001. Every call is advisory: nothing here changes a ticket or contacts a customer. Rate-limited per user on the server. */
@Injectable({ providedIn: 'root' })
export class AiApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  summary(ticketId: string): Observable<AiText> {
    return this.http.post<AiText>(this.apiUrl.versioned(`tickets/${ticketId}/ai/summary`), {});
  }

  replyDraft(ticketId: string, tone: AiReplyTone, instructions: string): Observable<AiText> {
    return this.http.post<AiText>(this.apiUrl.versioned(`tickets/${ticketId}/ai/reply-draft`), {
      tone,
      instructions: instructions.trim() || null,
    });
  }

  categorySuggestion(ticketId: string): Observable<AiCategorySuggestion> {
    return this.http.post<AiCategorySuggestion>(this.apiUrl.versioned(`tickets/${ticketId}/ai/category-suggestion`), {});
  }

  solutions(ticketId: string): Observable<AiSolutions> {
    return this.http.post<AiSolutions>(this.apiUrl.versioned(`tickets/${ticketId}/ai/solutions`), {});
  }

  chat(body: AiChatRequest): Observable<AiChatResponse> {
    return this.http.post<AiChatResponse>(this.apiUrl.versioned('ai/chat'), body);
  }
}
