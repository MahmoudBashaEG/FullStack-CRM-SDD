import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { API_CONFIG } from '../api/api-config';
import { AuthService } from './auth-service';

describe('AuthService', () => {
  it('keeps the access token in memory after a successful login', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: API_CONFIG, useValue: { baseUrl: '/api', version: 'v1' } },
      ],
    });
    const auth = TestBed.inject(AuthService);
    const http = TestBed.inject(HttpTestingController);

    auth.login({ email: 'customer@example.com', password: 'correct horse' }).subscribe();
    http.expectOne('/api/v1/auth/login').flush({
      accessToken: 'jwt-access-token',
      accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z',
      user: { id: '1', email: 'customer@example.com', firstName: 'Ada', lastName: 'Lovelace', phoneNumber: null, role: 'Customer' },
    });

    expect(auth.accessToken()).toBe('jwt-access-token');
    expect(auth.role()).toBe('Customer');
    expect(localStorage.length + sessionStorage.length).toBe(0);
    http.verify();
  });
});
