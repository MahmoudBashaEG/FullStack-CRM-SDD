import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController,provideHttpClientTesting } from '@angular/common/http/testing';
import { afterEach,beforeEach,describe,expect,it } from 'vitest';
import { AgentsService } from './agents.service';
describe('Agent data access uses the shared HTTP client',()=>{
  let service:AgentsService,http:HttpTestingController;
  const agent={userId:'id',displayName:'Agent',email:'agent@fixture.test',role:'Agent',isActive:true,assignmentReferenceId:'reference',version:'AAAAAAAAAAA='};
  beforeEach(()=>{TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});service=TestBed.inject(AgentsService);http=TestBed.inject(HttpTestingController);});afterEach(()=>http.verify());
  it('uses relative URLs, exact creation credentials and minimal versioned writes',async()=>{
    const create=service.create('Agent',agent.email,'  عربي password  ');const first=http.expectOne('/api/agents');expect(first.request.body).toEqual({displayName:'Agent',email:agent.email,password:'  عربي password  '});first.flush(agent);await create;
    const rename=service.rename('id','Renamed',agent.version);const second=http.expectOne('/api/agents/id');expect(second.request.method).toBe('PATCH');expect(second.request.body).toEqual({displayName:'Renamed',version:agent.version});second.flush(agent);await rename;
    const disable=service.setAvailability('id',false,agent.version);const third=http.expectOne('/api/agents/id/availability');expect(third.request.body).toEqual({isActive:false,version:agent.version});third.flush(agent);await disable;
    const list=service.list(2);const read=http.expectOne('/api/agents?page=2&pageSize=25');read.flush({items:[agent],page:2,pageSize:25,totalCount:26});await list;
  });
});
