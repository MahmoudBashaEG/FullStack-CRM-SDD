import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { AuthService } from './auth.service';
import { supportQueueGuard } from './support-queue.guard';
import { adminAgentsGuard } from './auth.guards';

describe('support role navigation',()=>{
  it('Admin management redirects visitors to signin and other roles to account access',()=>{
    let authenticated=false,managesAgents=false;TestBed.configureTestingModule({providers:[provideRouter([]),{provide:AuthService,useValue:{managesAgents:()=>managesAgents,authenticated:()=>authenticated}}]});
    const run=()=>TestBed.runInInjectionContext(()=>adminAgentsGuard({} as never,{} as never));expect(TestBed.inject(Router).serializeUrl(run() as UrlTree)).toBe('/auth/signin');authenticated=true;expect(TestBed.inject(Router).serializeUrl(run() as UrlTree)).toBe('/access');managesAgents=true;expect(run()).toBe(true);
  });
  it('permits only queue-authorized identities and redirects denied identity',()=>{
    let allowed=false;TestBed.configureTestingModule({providers:[provideRouter([]),{provide:AuthService,useValue:{allowed:()=>allowed,authenticated:()=>true}}]});
    const run=()=>TestBed.runInInjectionContext(()=>supportQueueGuard({} as never,{} as never));
    expect(TestBed.inject(Router).serializeUrl(run() as UrlTree)).toBe('/access');allowed=true;expect(run()).toBe(true);
  });
});
