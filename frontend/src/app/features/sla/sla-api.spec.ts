import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_CONFIG } from '../../core/api/api-config';
import { SlaApi } from './sla-api';
import { BusinessCalendar, UpdateBusinessCalendarRequest } from './sla-models';

describe('SlaApi', () => {
  it('saves the business calendar with working days, hours and holidays and returns the typed calendar', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: '/api', version: 'v1' } },
      ],
    });
    const api = TestBed.inject(SlaApi);
    const http = TestBed.inject(HttpTestingController);
    const payload: UpdateBusinessCalendarRequest = {
      timeZoneId: 'Africa/Cairo',
      workingDays: ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday'],
      opensAt: '09:00:00',
      closesAt: '17:00:00',
      holidays: [{ date: '2030-01-06', name: 'Example holiday' }],
    };
    let result: BusinessCalendar | undefined;

    api.saveCalendar(payload).subscribe(calendar => (result = calendar));
    const request = http.expectOne(r => r.url === '/api/v1/sla/calendar');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(payload);
    request.flush({ isConfigured: true, ...payload, updatedAtUtc: '2030-01-01T00:00:00Z', updatedByName: 'System Administrator' });

    expect(result?.isConfigured).toBe(true);
    expect(result?.holidays[0].date).toBe('2030-01-06');
    http.verify();
  });
});
