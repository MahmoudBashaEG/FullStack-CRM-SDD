import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import { PagedResponse } from './customer-models';
import { CustomerAttachment, CustomerInteraction, CustomerNote, LogCustomerInteractionRequest } from './customer-records-models';

/** Staff-only notes, interactions and attachments of a customer (CUST-002). */
@Injectable({ providedIn: 'root' })
export class CustomerRecordsApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  notes(customerId: string, page: number, pageSize: number): Observable<PagedResponse<CustomerNote>> {
    return this.http.get<PagedResponse<CustomerNote>>(this.apiUrl.versioned(`customers/${customerId}/notes`), { params: paging(page, pageSize) });
  }

  addNote(customerId: string, body: string): Observable<CustomerNote> {
    return this.http.post<CustomerNote>(this.apiUrl.versioned(`customers/${customerId}/notes`), { body });
  }

  interactions(customerId: string, page: number, pageSize: number): Observable<PagedResponse<CustomerInteraction>> {
    return this.http.get<PagedResponse<CustomerInteraction>>(this.apiUrl.versioned(`customers/${customerId}/interactions`), {
      params: paging(page, pageSize),
    });
  }

  logInteraction(customerId: string, body: LogCustomerInteractionRequest): Observable<CustomerInteraction> {
    return this.http.post<CustomerInteraction>(this.apiUrl.versioned(`customers/${customerId}/interactions`), body);
  }

  attachments(customerId: string, page: number, pageSize: number): Observable<PagedResponse<CustomerAttachment>> {
    return this.http.get<PagedResponse<CustomerAttachment>>(this.apiUrl.versioned(`customers/${customerId}/attachments`), {
      params: paging(page, pageSize),
    });
  }

  /** multipart/form-data with one 'file' field. Do not set Content-Type: the browser adds the boundary. */
  uploadAttachment(customerId: string, file: File): Observable<CustomerAttachment> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<CustomerAttachment>(this.apiUrl.versioned(`customers/${customerId}/attachments`), form);
  }

  /** Authenticated download (the interceptor adds the bearer token); there is no public file URL. */
  downloadAttachment(customerId: string, attachmentId: string): Observable<Blob> {
    return this.http.get(this.apiUrl.versioned(`customers/${customerId}/attachments/${attachmentId}/content`), { responseType: 'blob' });
  }
}

function paging(page: number, pageSize: number): HttpParams {
  return new HttpParams().set('page', page).set('pageSize', pageSize);
}
