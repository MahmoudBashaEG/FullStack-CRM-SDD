import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { describe, expect, it, vi } from 'vitest';
import { SigninPage } from './signin.page';
import { AuthService } from '../../../core/auth/auth.service';
describe('local signin form',()=>{
  it.each(['user@fixture.test','\ufeffuser@fixture.test\ufeff','\u0085"multi part"@example.test\u0085'])('preserves email %j and exact password through the form',async email=>{
    const signIn=vi.fn(async()=>undefined);TestBed.configureTestingModule({imports:[SigninPage],providers:[provideRouter([]),{provide:AuthService,useValue:{signIn,allowed:()=>false}}]});
    const navigate=vi.spyOn(TestBed.inject(Router),'navigateByUrl').mockResolvedValue(true);const fixture=TestBed.createComponent(SigninPage);fixture.detectChanges();
    const page=fixture.componentInstance;page.email=email;page.password='  exact عربي  password  ';await page.submit();expect(signIn).toHaveBeenCalledWith(email,'  exact عربي  password  ');expect(page.password).toBe('');expect(navigate).toHaveBeenCalledWith('/access');
    expect(fixture.nativeElement.querySelector('input[type="password"]').getAttribute('maxlength')).toBeNull();
  });
  it('shows generic credential failures without retaining the password',async()=>{
    TestBed.configureTestingModule({imports:[SigninPage],providers:[provideRouter([]),{provide:AuthService,useValue:{signIn:async()=>{throw new HttpErrorResponse({status:401});},allowed:()=>true}}]});
    const fixture=TestBed.createComponent(SigninPage);fixture.componentInstance.password='private typed input';await fixture.componentInstance.submit();fixture.detectChanges();expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('Email or password is incorrect.');expect(fixture.componentInstance.password).toBe('');
  });
});
