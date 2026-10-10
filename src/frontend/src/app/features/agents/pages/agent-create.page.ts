import { Component,inject,signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router,RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { trimAccountWhitespace, displayNameAccepted,normalizeEmail,passwordAccepted } from '../../../core/auth/account-validation';
import { AgentsService } from '../data-access/agents.service';
@Component({imports:[FormsModule,RouterLink],template:`<h1>Create Agent</h1><p>Create an active Agent with an initial password. This does not sign the Agent in.</p>
<form (ngSubmit)="submit()" aria-label="Create Agent" dir="auto">
<label>Display name <input name="displayName" autocomplete="name" required [(ngModel)]="displayName"></label>
<label>Email <input name="email" type="email" autocomplete="off" required [(ngModel)]="email"></label>
<label>Password <input name="password" type="password" autocomplete="new-password" required [(ngModel)]="password" aria-describedby="password-policy"></label>
<p id="password-policy">Use 15–128 characters. Spaces and Unicode are allowed; no character mixture is required.</p>
<button type="submit" [disabled]="busy()">Create Agent</button>@if(message()){<p role="alert">{{message()}}</p>}</form><p><a routerLink="/agents">Agents</a></p>`,styles:[`form{display:grid;gap:1rem;max-width:30rem}label{display:grid;gap:.4rem}input,button{padding:.7rem;max-width:100%;box-sizing:border-box}`]})
export class AgentCreatePage {
  private readonly service=inject(AgentsService);private readonly router=inject(Router);
  displayName='';email='';password='';readonly busy=signal(false);readonly message=signal('');
  async submit():Promise<void>{
    if(this.busy())return;this.message.set('');const email=normalizeEmail(this.email);
    if(!displayNameAccepted(this.displayName)||!email||!passwordAccepted(this.password)){this.message.set('Enter a nonblank display name of at most 200 characters, a valid email and a password of 15–128 characters.');return;}
    this.busy.set(true);try{const agent=await this.service.create(trimAccountWhitespace(this.displayName),email,this.password);await this.router.navigateByUrl(`/agents/${agent.userId}`);}
    catch(error){this.message.set(error instanceof HttpErrorResponse&&error.status===403?'You do not have permission to manage Agents.':error instanceof HttpErrorResponse&&[400,409].includes(error.status)?'Agent could not be created. Review the supplied details.':'Agent creation is unavailable. Try again.');}
    finally{this.password='';this.busy.set(false);}
  }
}
