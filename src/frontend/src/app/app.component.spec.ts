import { TestBed } from '@angular/core/testing';
import { App } from './app.component';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { AuthService } from './core/auth/auth.service';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]),provideHttpClient()],
    })
      .compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render title', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('header')?.textContent).toContain('Customer Support CRM');
  });
  it('does not show support navigation before authentication',()=>{
    const fixture=TestBed.createComponent(App);fixture.detectChanges();expect(fixture.nativeElement.querySelector('a[href="/tickets"]')).toBeNull();
  });
  it('keeps an explicit signout retry available after an unconfirmed failure',async()=>{
    TestBed.overrideProvider(AuthService,{useValue:{allowed:()=>false,managesAgents:()=>false,authenticated:()=>false,signOut:async()=>{throw new Error('Unavailable');}}});
    const fixture=TestBed.createComponent(App);await fixture.componentInstance.signOut();fixture.detectChanges();expect(fixture.nativeElement.textContent).toContain('Signout could not be confirmed.');expect(fixture.nativeElement.querySelector('button')?.textContent).toContain('Retry signout');
  });
});
