import { Component,inject,signal,OnInit,DestroyRef } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute,RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { trimAccountWhitespace, displayNameAccepted } from '../../../core/auth/account-validation';
import { AgentsService } from '../data-access/agents.service';
import { AgentDto } from '../models/agent.models';
@Component({imports:[FormsModule,RouterLink],template:`<h1>Manage Agent</h1>
@if(agent();as current){<p dir="auto">Current name: {{current.displayName}}</p><p>Email: {{current.email}}</p><p>{{current.isActive?'Active':'Disabled'}}</p>
<form (ngSubmit)="saveName()" aria-label="Edit Agent name" dir="auto"><label>Display name <input name="displayName" required [(ngModel)]="displayName"></label><button type="submit" [disabled]="busy()||stale()">Save name</button></form>
<form (ngSubmit)="saveAvailability()" aria-label="Agent availability"><label>Agent active <input name="isActive" type="checkbox" [(ngModel)]="isActive"></label><button type="submit" [disabled]="busy()||stale()">Save availability</button></form>}
@if(message()){<p role="status">{{message()}}</p>}
@if(stale()){<p role="alert">This Agent changed. Reload and review your draft before saving.</p><button (click)="reload()" [disabled]="busy()">Reload latest details</button>}
@if(!agent()&&!busy()){<button (click)="load()">Retry Agent</button>}
<p><a routerLink="/agents">Agents</a></p>`,styles:[`form{display:grid;gap:1rem;max-width:30rem;margin-block:1rem}label{display:grid;gap:.4rem}input,button{padding:.7rem;max-width:100%;box-sizing:border-box}`]})
export class AgentEditPage implements OnInit {
  private readonly service=inject(AgentsService);private readonly route=inject(ActivatedRoute);private readonly destroy=inject(DestroyRef);private id=this.route.snapshot.paramMap.get('userId')??'';private generation=0;
  readonly agent=signal<AgentDto|null>(null);readonly busy=signal(false);readonly stale=signal(false);readonly message=signal('');displayName='';isActive=true;
  ngOnInit(){this.route.paramMap.pipe(takeUntilDestroyed(this.destroy)).subscribe(params=>{const id=params.get('userId')??'';if(id!==this.id){this.generation++;this.id=id;this.agent.set(null);this.displayName='';this.stale.set(false);}if(!this.agent())void this.load();});}
  load():Promise<void>{return this.read(false);}
  reload():Promise<void>{return this.read(true);}
  private async read(preserveDraft:boolean):Promise<void>{const generation=++this.generation;this.busy.set(true);this.message.set('');try{const agent=await this.service.get(this.id);if(generation!==this.generation)return;this.agent.set(agent);if(!preserveDraft){this.displayName=agent.displayName;this.isActive=agent.isActive;}this.stale.set(false);if(preserveDraft)this.message.set('Latest details loaded. Review your draft before saving.');}catch(error){if(generation===this.generation)this.failure(error);}finally{if(generation===this.generation)this.busy.set(false);}}
  saveName():Promise<void>{if(!displayNameAccepted(this.displayName)){this.message.set('A nonblank display name of at most 200 characters is required.');return Promise.resolve();}return this.save(false);}
  saveAvailability():Promise<void>{return this.save(true);}
  private async save(availability:boolean):Promise<void>{const agent=this.agent();if(!agent||this.busy()||this.stale())return;const generation=this.generation;this.busy.set(true);this.message.set('');try{const result=availability?await this.service.setAvailability(agent.userId,this.isActive,agent.version):await this.service.rename(agent.userId,trimAccountWhitespace(this.displayName),agent.version);if(generation!==this.generation)return;this.agent.set(result);if(!availability)this.displayName=result.displayName;this.message.set('Saved.');}catch(error){if(generation===this.generation)this.failure(error);}finally{if(generation===this.generation)this.busy.set(false);}}
  private failure(error:unknown){if(error instanceof HttpErrorResponse&&error.status===409){this.stale.set(true);this.message.set('Your draft has been preserved.');}else this.message.set(error instanceof HttpErrorResponse&&error.status===403?'You do not have permission to manage Agents.':error instanceof HttpErrorResponse&&error.status===404?'Agent was not found.':'Agent operation failed. Try again.');}
}
