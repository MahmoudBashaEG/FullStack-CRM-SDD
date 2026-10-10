import { Component,inject,signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router,RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { SignupService } from '../data-access/signup.service';
import { trimAccountWhitespace, displayNameAccepted,normalizeEmail,passwordAccepted } from '../../../core/auth/account-validation';
@Component({imports:[FormsModule,RouterLink],template:`<h1>Create Customer account</h1>
  <p>Registration creates a Customer account. Sign in separately after registration.</p>
  <form (ngSubmit)="submit()" aria-label="Create Customer account" dir="auto">
    <label>Display name <input name="displayName" autocomplete="name" required [(ngModel)]="displayName"></label>
    <label>Email <input name="email" type="email" autocomplete="username" required [(ngModel)]="email"></label>
    <label>Password <input name="password" type="password" autocomplete="new-password" required [(ngModel)]="password" aria-describedby="password-policy"></label>
    <p id="password-policy">Use 15–128 characters. Spaces and Unicode are allowed; no character mixture is required.</p>
    <button type="submit" [disabled]="busy()">Create account</button>
    @if(message()){<p role="alert">{{message()}}</p>}
  </form><p><a routerLink="/auth/signin">Sign in</a></p>`,styles:[`form{display:grid;gap:1rem;max-width:30rem}label{display:grid;gap:.4rem}input,button{padding:.7rem;max-width:100%;box-sizing:border-box}`]})
export class SignupPage {
  private readonly signup=inject(SignupService);private readonly router=inject(Router);
  displayName='';email='';password='';
  readonly busy=signal(false);readonly message=signal('');
  async submit():Promise<void>{
    if(this.busy())return;this.message.set('');const email=normalizeEmail(this.email);
    if(!displayNameAccepted(this.displayName)||!email||!passwordAccepted(this.password)){
      this.message.set('Enter a nonblank display name of at most 200 characters, a valid email and a password of 15–128 characters.');return;
    }
    this.busy.set(true);
    try{await this.signup.register(trimAccountWhitespace(this.displayName),email,this.password);await this.router.navigateByUrl('/auth/signin');}
    catch(error){this.message.set(error instanceof HttpErrorResponse && error.status===400?'Registration could not be completed.':'Registration is unavailable. Try again.');}
    finally{this.password='';this.busy.set(false);}
  }
}
