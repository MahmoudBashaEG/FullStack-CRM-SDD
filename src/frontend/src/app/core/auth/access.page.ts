import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from './auth.service';
@Component({imports:[RouterLink],template:`<h1>Account access</h1>
  @if(auth.identity(); as user){<p dir="auto">{{user.displayName}} ({{user.role}})</p><p>Your account is signed in.</p>}
  @if(auth.status()==='unavailable'){<p role="alert">Authentication is unavailable. Retry or sign in again. No authenticated access is assumed.</p><button (click)="retry()">Retry authentication</button>}
  @if(auth.allowed()){<a routerLink="/tickets/new">Support queue</a>}
  @if(!auth.authenticated()){<a routerLink="/auth/signin">Sign in</a>}
  @if(message()){<p role="alert">{{message()}}</p>}`})
export class AccessPage {
  readonly auth=inject(AuthService);readonly message=signal('');
  async retry(){try{await this.auth.refresh();}catch{this.message.set('Authentication could not be restored.');}}
  async signOut(){try{await this.auth.signOut();}catch{this.message.set('Signout could not be confirmed. Try again.');}}
}
