import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AgentDto,AgentPage } from '../models/agent.models';
@Injectable({providedIn:'root'})
export class AgentsService {
  constructor(private readonly http:HttpClient){}
  list(page=1):Promise<AgentPage>{return firstValueFrom(this.http.get<AgentPage>('/api/agents',{params:{page,pageSize:25}}));}
  create(displayName:string,email:string,password:string):Promise<AgentDto>{return firstValueFrom(this.http.post<AgentDto>('/api/agents',{displayName,email,password}));}
  get(id:string):Promise<AgentDto>{return firstValueFrom(this.http.get<AgentDto>(`/api/agents/${encodeURIComponent(id)}`));}
  rename(id:string,displayName:string,version:string):Promise<AgentDto>{return firstValueFrom(this.http.patch<AgentDto>(`/api/agents/${encodeURIComponent(id)}`,{displayName,version}));}
  setAvailability(id:string,isActive:boolean,version:string):Promise<AgentDto>{return firstValueFrom(this.http.put<AgentDto>(`/api/agents/${encodeURIComponent(id)}/availability`,{isActive,version}));}
}
