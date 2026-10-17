import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_CONFIG } from '../../core/api/api-config';
import { Customer, PagedResponse } from './customer-models';
import { CustomersApi } from './customers-api';

describe('CustomersApi', () => {
  it('requests a one-based page with a trimmed search term and returns the typed page', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: '/api', version: 'v1' } },
      ],
    });
    const api = TestBed.inject(CustomersApi);
    const http = TestBed.inject(HttpTestingController);
    let result: PagedResponse<Customer> | undefined;

    api.list({ page: 2, pageSize: 20, search: '  ada ' }).subscribe(page => (result = page));
    const request = http.expectOne(r => r.url === '/api/v1/customers');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('20');
    expect(request.request.params.get('search')).toBe('ada');
    request.flush({
      items: [
        {
          id: 'c1', firstName: 'Ada', lastName: 'Lovelace', email: 'ada@example.com', phoneNumber: null,
          company: 'Analytical Engines', isRegistered: false,
          createdAtUtc: '2030-01-01T00:00:00Z', updatedAtUtc: '2030-01-01T00:00:00Z',
        },
      ],
      page: 2,
      pageSize: 20,
      totalCount: 21,
    });

    expect(result?.totalCount).toBe(21);
    expect(result?.items[0].isRegistered).toBe(false);
    http.verify();
  });
});
