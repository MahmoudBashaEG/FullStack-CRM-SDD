import { Component, inject, signal } from '@angular/core';
import { AuthService } from './auth.service';

@Component({ template: `<h1>Support queue access</h1><p role="status">{{ message() }}</p><button type="button" (click)="signIn()">Sign in</button>` })
export class AccessPage {
  private readonly auth = inject(AuthService);
  readonly message = signal(this.auth.authenticated() ? 'Your account does not have shared queue access.' : 'Sign in with an authorized support account.');
  async signIn(): Promise<void> { try { await this.auth.signIn(); } catch { this.message.set('External sign-in is not configured or could not be started.'); } }
}
