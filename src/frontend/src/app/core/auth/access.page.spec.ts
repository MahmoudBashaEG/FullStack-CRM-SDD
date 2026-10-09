import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { AccessPage } from './access.page';
import { AuthService } from './auth.service';
describe('Customer identity access',()=>{
  it('shows safe local identity without portal or queue controls',()=>{
    TestBed.configureTestingModule({imports:[AccessPage],providers:[provideRouter([]),{provide:AuthService,useValue:{identity:()=>({userId:'id',displayName:'Customer عربي',role:'Customer'}),status:()=> 'authenticated',allowed:()=>false,authenticated:()=>true}}]});
    const fixture=TestBed.createComponent(AccessPage);fixture.detectChanges();expect(fixture.nativeElement.textContent).toContain('Customer عربي');expect(fixture.nativeElement.textContent).not.toContain('Support queue');
  });
});
