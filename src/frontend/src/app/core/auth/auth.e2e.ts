import { Injectable, signal } from '@angular/core';

declare global { interface Window { __CRM_TEST_AUTH?: { token: string; allowed: boolean }; } }
@Injectable({ providedIn: 'root' })
export class AuthService {
  private token: string | null = null;
  readonly ready = signal(false);
  readonly authenticated = signal(false);
  readonly allowed = signal(false);
  async initialize(): Promise<void> {
    const fixture = window.__CRM_TEST_AUTH;
    this.token = fixture?.token ?? null;
    this.authenticated.set(!!this.token);
    this.allowed.set(fixture?.allowed ?? false);
    delete window.__CRM_TEST_AUTH;
    this.ready.set(true);
  }
  accessToken(): string | null { return this.token; }
  async signIn(): Promise<void> { throw new Error('External login is unavailable in browser tests.'); }
}
