import { TestBed } from '@angular/core/testing';
import { provideRouter,Router,ActivatedRoute,convertToParamMap } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { of } from 'rxjs';
import { describe,expect,it,vi } from 'vitest';
import { AgentsService } from '../data-access/agents.service';
import { AgentCreatePage } from './agent-create.page';
import { AgentListPage } from './agent-list.page';
import { AgentEditPage } from './agent-edit.page';
const agent={userId:'10000000-0000-0000-0000-000000000001',displayName:'Agent عربي',email:'agent@fixture.test',role:'Agent' as const,isActive:true,assignmentReferenceId:'20000000-0000-0000-0000-000000000001',version:'AAAAAAAAAAA='};
describe('Admin Agent management',()=>{
  it.each([' \t','\u0085','\u00a0\u202f'])('normalizes Agent-create boundary name after %j',async whitespace=>{
    const {service}=setup(AgentCreatePage);const page=TestBed.createComponent(AgentCreatePage).componentInstance;page.displayName=whitespace+'😀'.repeat(200)+whitespace;page.email='\ufeffagent@example.test\ufeff';page.password='  عربي password  ';await page.submit();expect(service.create).toHaveBeenCalledWith('😀'.repeat(200),page.email,'  عربي password  ');
  });
  it.each([' \t','\u0085','\u00a0\u202f'])('normalizes Agent-rename boundary name after %j',async whitespace=>{
    const {service}=setup(AgentEditPage);const page=TestBed.createComponent(AgentEditPage).componentInstance;await page.load();page.displayName=whitespace+'😀'.repeat(200)+whitespace;await page.saveName();expect(service.rename).toHaveBeenCalledWith(agent.userId,'😀'.repeat(200),agent.version);
  });
  it.each(['\u0085','\u0085'+'a'.repeat(201)+'\u0085','\ud800'])('rejects invalid normalized Agent names %j before creation/rename',async name=>{
    const {service}=setup(AgentCreatePage);const page=TestBed.createComponent(AgentCreatePage).componentInstance;page.displayName=name;page.email=agent.email;page.password='123456789012345';await page.submit();expect(service.create).not.toHaveBeenCalled();TestBed.resetTestingModule();
    const edit=setup(AgentEditPage);const editor=TestBed.createComponent(AgentEditPage).componentInstance;await editor.load();editor.displayName=name;await editor.saveName();expect(edit.service.rename).not.toHaveBeenCalled();
  });
  it.each(['  ','\u0085','\u00a0\u202f'])('submits a quoted Agent address after trimming %j without removing internal spaces',async whitespace=>{
    const {service,navigate}=setup(AgentCreatePage);const page=TestBed.createComponent(AgentCreatePage).componentInstance;page.displayName='Agent';page.email=whitespace+'"multi part"@example.test'+whitespace;page.password='  عربي password  ';await page.submit();
    expect(service.create).toHaveBeenCalledWith('Agent','"multi part"@example.test','  عربي password  ');expect(navigate).toHaveBeenCalledWith(`/agents/${agent.userId}`);
  });
  function setup(component:typeof AgentCreatePage|typeof AgentListPage|typeof AgentEditPage,overrides:object={}){
    const service={list:vi.fn(async()=>({items:[agent],page:1,pageSize:25,totalCount:1})),create:vi.fn(async()=>agent),get:vi.fn(async()=>agent),rename:vi.fn(async()=>({...agent,version:'AAAAAAAAAAE='})),setAvailability:vi.fn(async()=>({...agent,isActive:false,version:'AAAAAAAAAAE='})),...overrides};
    TestBed.configureTestingModule({imports:[component],providers:[provideRouter([]),{provide:AgentsService,useValue:service},{provide:ActivatedRoute,useValue:{paramMap:of(convertToParamMap({userId:agent.userId})),snapshot:{paramMap:convertToParamMap({userId:agent.userId})}}}]});
    const navigate=vi.spyOn(TestBed.inject(Router),'navigateByUrl').mockResolvedValue(true);return{service,navigate};
  }
  it('creates only Agent input with exact Unicode/spaces password and clears it',async()=>{
    const {service,navigate}=setup(AgentCreatePage);const fixture=TestBed.createComponent(AgentCreatePage);fixture.detectChanges();const page=fixture.componentInstance;page.displayName=' Agent عربي ';page.email=' agent@fixture.test ';page.password='  عربي password  ';await page.submit();fixture.detectChanges();
    expect(service.create).toHaveBeenCalledWith('Agent عربي','agent@fixture.test','  عربي password  ');expect(page.password).toBe('');expect(navigate).toHaveBeenCalledWith(`/agents/${agent.userId}`);expect(fixture.nativeElement.querySelector('select')).toBeNull();expect(fixture.nativeElement.querySelector('input[type="password"]').getAttribute('maxlength')).toBeNull();
  });
  it.each([14,129])('rejects %i scalars before submitting',async count=>{const {service}=setup(AgentCreatePage);const page=TestBed.createComponent(AgentCreatePage).componentInstance;page.displayName='Agent';page.email=agent.email;page.password='😀'.repeat(count);await page.submit();expect(service.create).not.toHaveBeenCalled();});
  it.each([15,128])('accepts %i supplementary scalars unchanged',async count=>{const {service}=setup(AgentCreatePage);const page=TestBed.createComponent(AgentCreatePage).componentInstance;page.displayName='Agent';page.email=agent.email;page.password='😀'.repeat(count);await page.submit();expect(service.create).toHaveBeenCalledWith('Agent',agent.email,'😀'.repeat(count));});
  it('clears attempted password and does not expose server internals on conflict',async()=>{
    setup(AgentCreatePage,{create:vi.fn(async()=>{throw new HttpErrorResponse({status:409,error:{title:'private internals'}});})});const fixture=TestBed.createComponent(AgentCreatePage);const page=fixture.componentInstance;page.displayName='Agent';page.email=agent.email;page.password='123456789012345';await page.submit();fixture.detectChanges();expect(page.password).toBe('');expect(fixture.nativeElement.textContent).toContain('Agent could not be created.');expect(fixture.nativeElement.textContent).not.toContain('private internals');
  });
  it('lists safe fields, includes disabled Agents and has no credential/role controls',async()=>{
    setup(AgentListPage,{list:vi.fn(async()=>({items:[{...agent,isActive:false}],page:1,pageSize:25,totalCount:1}))});const fixture=TestBed.createComponent(AgentListPage);await fixture.componentInstance.load();fixture.detectChanges();expect(fixture.nativeElement.textContent).toContain(agent.email);expect(fixture.nativeElement.textContent).toContain('Disabled');expect(fixture.nativeElement.querySelector('input[type="password"]')).toBeNull();
  });
  it('edits name/availability with version, without email/password/role/delete controls',async()=>{
    const {service}=setup(AgentEditPage);const fixture=TestBed.createComponent(AgentEditPage);const page=fixture.componentInstance;await page.load();page.displayName=' Renamed عربي ';await page.saveName();page.isActive=false;await page.saveAvailability();fixture.detectChanges();expect(service.rename).toHaveBeenCalledWith(agent.userId,'Renamed عربي',agent.version);expect(service.setAvailability).toHaveBeenCalledWith(agent.userId,false,'AAAAAAAAAAE=');expect(fixture.nativeElement.querySelector('input[type="email"],input[type="password"],select')).toBeNull();expect(fixture.nativeElement.textContent).not.toContain('Delete');
  });
  it('preserves stale drafts until explicit reload/review, without overwrite retries',async()=>{
    const rename=vi.fn(async()=>{throw new HttpErrorResponse({status:409});});const {service}=setup(AgentEditPage,{rename});const fixture=TestBed.createComponent(AgentEditPage);const page=fixture.componentInstance;await page.load();page.displayName='My draft';page.isActive=false;await page.saveName();await page.saveName();expect(rename).toHaveBeenCalledTimes(1);fixture.detectChanges();expect(fixture.nativeElement.textContent).toContain('Reload');expect(page.displayName).toBe('My draft');service.get.mockResolvedValue({...agent,displayName:'Other edit',version:'AAAAAAAAAAI='});await page.reload();expect(page.displayName).toBe('My draft');expect(page.isActive).toBe(false);fixture.detectChanges();expect(fixture.nativeElement.textContent).toContain('Other edit');
  });
});
