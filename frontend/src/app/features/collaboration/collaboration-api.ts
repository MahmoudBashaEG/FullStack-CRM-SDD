import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import { PagedResponse } from '../customers/customer-models';
import {
  CollaborationTicket,
  CollaboratorCandidate,
  MyCollaboration,
  TicketCollaborator,
  TicketMessage,
} from './collaboration-models';

/** The internal-discussion surface. It never calls /api/v1/tickets. */
@Injectable({ providedIn: 'root' })
export class CollaborationApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  mine(page: number, pageSize: number): Observable<PagedResponse<MyCollaboration>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResponse<MyCollaboration>>(this.apiUrl.versioned('collaborations'), { params });
  }

  ticket(ticketId: string): Observable<CollaborationTicket> {
    return this.http.get<CollaborationTicket>(this.apiUrl.versioned(`collaborations/${ticketId}`));
  }

  /** Newest first. */
  messages(ticketId: string, page: number, pageSize: number): Observable<PagedResponse<TicketMessage>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResponse<TicketMessage>>(this.apiUrl.versioned(`collaborations/${ticketId}/messages`), { params });
  }

  post(ticketId: string, body: string): Observable<TicketMessage> {
    return this.http.post<TicketMessage>(this.apiUrl.versioned(`collaborations/${ticketId}/messages`), { body });
  }

  collaborators(ticketId: string): Observable<TicketCollaborator[]> {
    return this.http.get<TicketCollaborator[]>(this.apiUrl.versioned(`collaborations/${ticketId}/collaborators`));
  }

  candidates(ticketId: string): Observable<CollaboratorCandidate[]> {
    return this.http.get<CollaboratorCandidate[]>(this.apiUrl.versioned(`collaborations/${ticketId}/eligible-agents`));
  }

  invite(ticketId: string, agentUserId: string): Observable<TicketCollaborator> {
    return this.http.post<TicketCollaborator>(this.apiUrl.versioned(`collaborations/${ticketId}/collaborators`), { agentUserId });
  }

  remove(ticketId: string, agentUserId: string): Observable<void> {
    return this.http.delete<void>(this.apiUrl.versioned(`collaborations/${ticketId}/collaborators/${agentUserId}`));
  }
}
