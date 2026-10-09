import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router,RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../../core/auth/auth.service';

@Component({imports:[FormsModule,RouterLink],template:`<h1>Sign in</h1>
  <form (ngSubmit)="submit()" aria-label="Sign in" dir="auto">
    <label>Email <input name="email" type="email" autocomplete="username" required [(ngModel)]="email"></label>
    <label>Password <input name="password" type="password" autocomplete="current-password" required [(ngModel)]="password"></label>
    <button type="submit" [disabled]="busy()">Sign in</button>
    @if(message()){<p role="alert">{{message()}}</p>}
  </form><p><a routerLink="/auth/signup">Create Customer account</a></p>`,styles:[`form{display:grid;gap:1rem;max-width:30rem}label{display:grid;gap:.4rem}input,button{padding:.7rem;max-width:100%;box-sizing:border-box}`]})
export class SigninPage {
  private readonly auth=inject(AuthService);private readonly router=inject(Router);
  email='';password='';readonly busy=signal(false);readonly message=signal('');
  async submit() {
    if(this.busy())return;this.busy.set(true);this.message.set('');
    try {await this.auth.signIn(this.email,this.password);await this.router.navigateByUrl(this.auth.allowed()?'/tickets/new':'/access');}
    catch(error){this.message.set(error instanceof HttpErrorResponse && [400,401].includes(error.status)?'Email or password is incorrect.':'Authentication is unavailable. Try again.');}
    finally{this.password='';this.busy.set(false);}
  }
}
