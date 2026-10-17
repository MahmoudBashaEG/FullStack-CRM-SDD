import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_CONFIG } from '../../core/api/api-config';
import { AgentDashboardApi } from './agent-dashboard-api';
import { AgentDashboard } from './agent-dashboard-models';

describe('AgentDashboardApi', () => {
  it('requests the caller-scoped dashboard without any agent parameter and returns the typed snapshot', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: '/api', version: 'v1' } },
      ],
    });
    const api = TestBed.inject(AgentDashboardApi);
    const http = TestBed.inject(HttpTestingController);
    let result: AgentDashboard | undefined;

    api.get().subscribe(d => (result = d));

    const request = http.expectOne(r => r.url === '/api/v1/agent-dashboard');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.keys()).toEqual([]); // ownership comes from the token only
    request.flush({
      summary: { open: 3, inProgress: 2, active: 5, escalated: 1, highOrUrgent: 2 },
      attentionQueue: [
        {
          id: 't1',
          subject: 'Cannot sign in',
          category: 'Technical',
          priority: 'Urgent',
          status: 'InProgress',
          isEscalated: true,
          customerId: 'c1',
          customerName: 'Ada Lovelace',
          customerEmail: 'ada@example.com',
          updatedAtUtc: '2030-01-01T09:00:00Z',
        },
      ],
      attentionQueueLimit: 10,
      generatedAtUtc: '2030-01-01T10:00:00Z',
    });

    expect(result?.summary.active).toBe(5);
    expect(result?.attentionQueue[0].customerName).toBe('Ada Lovelace');
    expect(result?.attentionQueueLimit).toBe(10);
    http.verify();
  });
});
