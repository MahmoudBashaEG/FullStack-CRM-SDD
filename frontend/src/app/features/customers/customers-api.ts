import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import {
  CreateCustomerRequest,
  Customer,
  CustomerListParams,
  PagedResponse,
  UpdateCustomerRequest,
} from './customer-models';

@Injectable({ providedIn: 'root' })
export class CustomersApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  list({ page, pageSize, search }: CustomerListParams): Observable<PagedResponse<Customer>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    const term = search?.trim();
    if (term) {
      params = params.set('search', term);
    }
    return this.http.get<PagedResponse<Customer>>(this.apiUrl.versioned('customers'), { params });
  }

  get(id: string): Observable<Customer> {
    return this.http.get<Customer>(this.apiUrl.versioned(`customers/${id}`));
  }

  create(body: CreateCustomerRequest): Observable<Customer> {
    return this.http.post<Customer>(this.apiUrl.versioned('customers'), body);
  }

  update(id: string, body: UpdateCustomerRequest): Observable<Customer> {
    return this.http.put<Customer>(this.apiUrl.versioned(`customers/${id}`), body);
  }
}
