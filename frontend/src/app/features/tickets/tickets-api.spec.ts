import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_CONFIG } from '../../core/api/api-config';
import { PagedResponse } from '../customers/customer-models';
import { Ticket } from './ticket-models';
import { TicketsApi } from './tickets-api';

describe('TicketsApi', () => {
  it('requests a filtered one-based page, omits empty filters and returns the typed page', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: '/api', version: 'v1' } },
      ],
    });
    const api = TestBed.inject(TicketsApi);
    const http = TestBed.inject(HttpTestingController);
    let result: PagedResponse<Ticket> | undefined;

    api
      .list({ page: 1, pageSize: 20, search: '  login ', customerId: 'c1', status: 'Open', category: null, priority: 'High' })
      .subscribe(page => (result = page));
    const request = http.expectOne(r => r.url === '/api/v1/tickets');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('20');
    expect(request.request.params.get('search')).toBe('login');
    expect(request.request.params.get('customerId')).toBe('c1');
    expect(request.request.params.get('status')).toBe('Open');
    expect(request.request.params.get('priority')).toBe('High');
    expect(request.request.params.has('category')).toBe(false);
    request.flush({
      items: [
        {
          id: 't1', subject: 'Cannot sign in', description: 'Password rejected.', category: 'Technical', priority: 'High',
          status: 'Open', customerId: 'c1', customerName: 'Ada Lovelace', customerEmail: 'ada@example.com',
          createdByUserId: 'a1', createdByName: 'Agent One', assignedAgentUserId: 'a1', assignedAgentName: 'Agent One',
          createdAtUtc: '2030-01-01T00:00:00Z', updatedAtUtc: '2030-01-01T00:00:00Z',
        },
      ],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });

    expect(result?.totalCount).toBe(1);
    expect(result?.items[0].assignedAgentName).toBe('Agent One');
    http.verify();
  });
});
