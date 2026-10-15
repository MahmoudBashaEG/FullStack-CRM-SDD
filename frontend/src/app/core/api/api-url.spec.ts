import { TestBed } from '@angular/core/testing';
import { API_CONFIG } from './api-config';
import { ApiUrl } from './api-url';

describe('ApiUrl', () => {
  it('builds versioned business routes from the central config', () => {
    TestBed.configureTestingModule({
      providers: [{ provide: API_CONFIG, useValue: { baseUrl: '/api', version: 'v1' } }],
    });

    const apiUrl = TestBed.inject(ApiUrl);

    expect(apiUrl.versioned('tickets')).toBe('/api/v1/tickets');
  });
});
