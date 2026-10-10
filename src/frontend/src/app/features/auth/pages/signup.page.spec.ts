import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { describe, expect, it, vi } from 'vitest';
import { SignupPage } from './signup.page';
import { AuthService } from '../../../core/auth/auth.service';
describe('Customer signup',()=>{
  it.each([' \t','\u0085','\u00a0\u202f'])('submits a normalized boundary name after %j',whitespace=>{
    return (async()=>{const {page,registerCustomer}=setup();page.displayName=whitespace+'😀'.repeat(200)+whitespace;page.email='\ufeffuser@example.test\ufeff';page.password='  عربي password  ';await page.submit();expect(registerCustomer).toHaveBeenCalledWith('😀'.repeat(200),page.email,'  عربي password  ');})();
  });
  it.each(['\u0085','\u0085'+'a'.repeat(201)+'\u0085','\ud800'])('rejects invalid normalized names %j',async name=>{const {page,registerCustomer}=setup();page.displayName=name;page.email='user@example.test';page.password='123456789012345';await page.submit();expect(registerCustomer).not.toHaveBeenCalled();});
  it.each(['  ','\u0085','\u00a0\u202f'])('submits a quoted address intact after trimming %j and still requires explicit signin',async whitespace=>{
    const {page,registerCustomer,navigate}=setup();page.displayName='Customer';page.email=whitespace+'"multi part"@example.test'+whitespace;page.password='  عربي password  ';await page.submit();
    expect(registerCustomer).toHaveBeenCalledWith('Customer','"multi part"@example.test','  عربي password  ');expect(navigate).toHaveBeenCalledWith('/auth/signin');
  });
  function setup(registerCustomer=vi.fn(async():Promise<void>=>undefined)) {
    TestBed.configureTestingModule({imports:[SignupPage],providers:[provideRouter([]),{provide:AuthService,useValue:{registerCustomer}}]});
    const navigate=vi.spyOn(TestBed.inject(Router),'navigateByUrl').mockResolvedValue(true);
    const fixture=TestBed.createComponent(SignupPage);fixture.detectChanges();return{fixture,page:fixture.componentInstance,registerCustomer,navigate};
  }
  it('uses common inputs and exact password, then requires explicit signin',async()=>{
    const {fixture,page,registerCustomer,navigate}=setup();page.displayName=' Customer عربي ';page.email=' customer@fixture.test ';page.password='  عربي password  ';await page.submit();fixture.detectChanges();
    expect(registerCustomer).toHaveBeenCalledWith('Customer عربي','customer@fixture.test','  عربي password  ');expect(page.password).toBe('');expect(navigate).toHaveBeenCalledWith('/auth/signin');
    expect(fixture.nativeElement.querySelector('select')).toBeNull();expect(fixture.nativeElement.querySelector('input[type="password"]').getAttribute('maxlength')).toBeNull();
    expect(fixture.nativeElement.querySelector('form').getAttribute('dir')).toBe('auto');
  });
  it.each([14,129])('rejects %i scalars before sending',async length=>{
    const {page,registerCustomer,navigate}=setup();page.displayName='Customer';page.email='customer@fixture.test';page.password='😀'.repeat(length);await page.submit();expect(registerCustomer).not.toHaveBeenCalled();expect(navigate).not.toHaveBeenCalled();
  });
  it.each([15,128])('accepts %i supplementary scalars and all spaces unchanged',async length=>{
    const {page,registerCustomer}=setup();page.displayName='Customer';page.email='customer@fixture.test';
    for(const password of ['😀'.repeat(length),' '.repeat(length)]){page.password=password;await page.submit();expect(registerCustomer).toHaveBeenLastCalledWith('Customer','customer@fixture.test',password);}
  });
  it('shows generic registration failures, never server-sensitive text',async()=>{
    const {fixture,page,navigate}=setup(vi.fn(async()=>{throw new HttpErrorResponse({status:400,error:{code:'registration_failed',title:'private existing email'}});}));
    page.displayName='Customer';page.email='customer@fixture.test';page.password='123456789012345';await page.submit();fixture.detectChanges();expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('Registration could not be completed.');expect(fixture.nativeElement.textContent).not.toContain('private existing email');expect(page.password).toBe('');expect(navigate).not.toHaveBeenCalled();
  });
  it('prevents duplicate submissions and shows dependency failure safely',async()=>{
    let reject!:(error:unknown)=>void;const operation=new Promise<void>((_,failure)=>reject=failure);const {fixture,page,registerCustomer}=setup(vi.fn(()=>operation));page.displayName='Customer';page.email='customer@fixture.test';page.password='123456789012345';const first=page.submit();await page.submit();expect(registerCustomer).toHaveBeenCalledTimes(1);reject(new HttpErrorResponse({status:503}));await first;fixture.detectChanges();expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('Registration is unavailable. Try again.');
  });
});
