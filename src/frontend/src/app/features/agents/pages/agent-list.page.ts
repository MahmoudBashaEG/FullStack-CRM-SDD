import { Component,inject,signal,OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AgentsService } from '../data-access/agents.service';
import { AgentPage } from '../models/agent.models';
@Component({imports:[RouterLink],template:`<h1>Agents</h1><a routerLink="/agents/new">Create Agent</a>
@if(message()){<p role="alert">{{message()}}</p><button (click)="load()" [disabled]="busy()">Retry list</button>}
@if(data();as page){<div class="scroll"><table><caption>Active and disabled Agents</caption><thead><tr><th>Display name</th><th>Email</th><th>State</th><th>Manage</th></tr></thead><tbody>
@for(agent of page.items;track agent.userId){<tr><td dir="auto">{{agent.displayName}}</td><td>{{agent.email}}</td><td>{{agent.isActive?'Active':'Disabled'}}</td><td><a [routerLink]="['/agents',agent.userId]" [attr.aria-label]="'Edit '+agent.displayName">Edit</a></td></tr>}</tbody></table></div>
@if(page.items.length===0){<p>No Agents on this page.</p>}
<nav aria-label="Agent pages"><button (click)="load(page.page-1)" [disabled]="busy()||page.page<=1">Previous</button> Page {{page.page}} <button (click)="load(page.page+1)" [disabled]="busy()||page.page*page.pageSize>=page.totalCount">Next</button></nav>}`,styles:[`.scroll{overflow-x:auto}table{width:100%;border-collapse:collapse}th,td{text-align:start;padding:.6rem;border-bottom:1px solid #ccc}nav{margin-top:1rem}`]})
export class AgentListPage implements OnInit {
  private readonly service=inject(AgentsService);readonly data=signal<AgentPage|null>(null);readonly busy=signal(false);readonly message=signal('');private generation=0;
  ngOnInit(){if(!this.data())void this.load();}
  async load(page=this.data()?.page??1):Promise<void>{const generation=++this.generation;this.busy.set(true);this.message.set('');try{const result=await this.service.list(page);if(generation===this.generation)this.data.set(result);}catch(error){if(generation===this.generation)this.message.set(error instanceof HttpErrorResponse&&error.status===403?'You do not have permission to manage Agents.':'Agents could not be loaded.');}finally{if(generation===this.generation)this.busy.set(false);}}
}
