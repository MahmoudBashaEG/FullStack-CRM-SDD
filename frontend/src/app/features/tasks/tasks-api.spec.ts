import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_CONFIG } from '../../core/api/api-config';
import { PagedResponse } from '../customers/customer-models';
import { AgentTask } from './task-models';
import { TasksApi } from './tasks-api';

describe('TasksApi', () => {
  it('requests the default My Tasks page (Pending, unarchived) omitting empty filters and returns typed tasks with link state', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: '/api', version: 'v1' } },
      ],
    });
    const api = TestBed.inject(TasksApi);
    const http = TestBed.inject(HttpTestingController);
    let result: PagedResponse<AgentTask> | undefined;

    api
      .list({
        page: 1, pageSize: 20, search: '  call ', status: 'Pending', dueState: null, archived: false,
        hasReminder: null, assignedAgentUserId: null,
      })
      .subscribe(page => (result = page));
    const request = http.expectOne(r => r.url === '/api/v1/tasks');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('20');
    expect(request.request.params.get('search')).toBe('call');
    expect(request.request.params.get('status')).toBe('Pending');
    expect(request.request.params.get('archived')).toBe('false');
    expect(request.request.params.has('dueState')).toBe(false);
    expect(request.request.params.has('hasReminder')).toBe(false);
    expect(request.request.params.has('assignedAgentUserId')).toBe(false); // ownership comes from the token
    request.flush({
      items: [
        {
          id: 'k1', title: 'Call back customer', description: null, status: 'Pending',
          assignedAgentUserId: 'a1', assignedAgentName: 'Agent One', createdByUserId: 'a1', createdByName: 'Agent One',
          dueAtUtc: '2030-01-02T09:00:00Z', remindAtUtc: '2030-01-02T08:00:00Z', reminderSentAtUtc: null,
          linkedTicketState: 'Inaccessible', linkedTicketId: null, linkedTicketSubject: null, isOverdue: true, isDueSoon: false,
          createdAtUtc: '2030-01-01T09:00:00Z', updatedAtUtc: '2030-01-01T09:00:00Z', completedAtUtc: null,
          isArchived: false, archivedAtUtc: null, version: 'AAAAAAAAB9E=',
        },
      ],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });

    expect(result?.totalCount).toBe(1);
    expect(result?.items[0].linkedTicketState).toBe('Inaccessible');
    expect(result?.items[0].linkedTicketId).toBeNull();
    expect(result?.items[0].isOverdue).toBe(true);
    expect(result?.items[0].version).toBe('AAAAAAAAB9E='); // the concurrency token later sent as If-Match
    http.verify();
  });
});
