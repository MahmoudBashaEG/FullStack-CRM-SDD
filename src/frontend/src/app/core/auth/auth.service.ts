import { Injectable, signal } from '@angular/core';
import { UserManager, WebStorageStateStore } from 'oidc-client-ts';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly manager = environment.oidc.authority && environment.oidc.clientId
    ? new UserManager({ authority: environment.oidc.authority, client_id: environment.oidc.clientId,
      redirect_uri: new URL(environment.oidc.redirectPath, location.origin).href, response_type: 'code',
      scope: environment.oidc.scope, userStore: new WebStorageStateStore({ store: sessionStorage }) }) : null;
  readonly ready = signal(false);
  readonly authenticated = signal(false);
  readonly allowed = signal(false);
  private token: string | null = null;
  async initialize(): Promise<void> {
    try {
      if (!this.manager) return;
      const user = location.pathname === environment.oidc.redirectPath
        ? await this.manager.signinRedirectCallback() : await this.manager.getUser();
      if (user && !user.expired) {
        this.token = user.access_token;
        this.authenticated.set(true);
        this.allowed.set(user.profile['crm_support_queue'] === 'shared');
      }
      if (location.pathname === environment.oidc.redirectPath) location.replace('/tickets/new');
    } finally { this.ready.set(true); }
  }
  accessToken(): string | null { return this.token; }
  async signIn(): Promise<void> { if (!this.manager) throw new Error('External sign-in is not configured.'); await this.manager.signinRedirect(); }
}
