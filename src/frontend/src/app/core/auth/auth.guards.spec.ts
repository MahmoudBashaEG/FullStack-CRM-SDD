import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { AuthService } from './auth.service';
import { supportQueueGuard } from './support-queue.guard';

describe('support role navigation',()=>{
  it('permits only queue-authorized identities and redirects denied identity',()=>{
    let allowed=false;TestBed.configureTestingModule({providers:[provideRouter([]),{provide:AuthService,useValue:{allowed:()=>allowed,authenticated:()=>true}}]});
    const run=()=>TestBed.runInInjectionContext(()=>supportQueueGuard({} as never,{} as never));
    expect(TestBed.inject(Router).serializeUrl(run() as UrlTree)).toBe('/access');allowed=true;expect(run()).toBe(true);
  });
});
