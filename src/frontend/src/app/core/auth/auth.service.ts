import { Injectable, computed, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthStatus, CrmIdentity, TokenEnvelope } from './auth.models';
import { AuthCoordinator } from './auth-coordinator';
import { AuthTransport } from './auth.transport';

@Injectable({providedIn:'root'})
export class AuthService {
  readonly status = signal<AuthStatus>('initializing');
  readonly ready = signal(false);
  readonly identity = signal<CrmIdentity | null>(null);
  readonly authenticated = computed(() => this.status() === 'authenticated' && this.identity() !== null);
  readonly allowed = computed(() => this.authenticated() && ['Agent','Admin'].includes(this.identity()!.role));
  private token: string | null = null;
  private csrf: string | null = null;
  private readonly coordinator = new AuthCoordinator();
  private mutationQueue:Promise<void>=Promise.resolve();
  private mutations=0;
  constructor(private readonly transport: AuthTransport) {}
  accessToken(): string | null { return this.token; }
  generation(): number { return this.coordinator.generation; }
  private current(generation:number) { if(generation !== this.generation()) throw new Error('Authentication changed.'); }
  private async requestToken(generation:number): Promise<string> {
    if(!this.csrf) {
      const response = await this.transport.csrf(); this.current(generation);
      if(typeof response.requestToken !== 'string' || !response.requestToken) throw new Error('Authentication unavailable.');
      this.csrf=response.requestToken;
    }
    return this.csrf;
  }
  private validateEnvelope(envelope:TokenEnvelope) {
    if(!envelope || envelope.tokenType !== 'Bearer' || typeof envelope.accessToken !== 'string' || !envelope.accessToken || typeof envelope.expiresAtUtc !== 'string' || !Number.isFinite(Date.parse(envelope.expiresAtUtc)) || Date.parse(envelope.expiresAtUtc) <= Date.now()) throw new Error('Authentication unavailable.');
  }
  private validate(envelope:TokenEnvelope,identity:CrmIdentity) {
    this.validateEnvelope(envelope);
    if(!identity || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(identity.userId) || typeof identity.displayName !== 'string' || !['Customer','Agent','Admin'].includes(identity.role)) throw new Error('Authentication unavailable.');
  }
  private clear(status:AuthStatus) { this.token=null;this.identity.set(null);this.status.set(status);this.ready.set(true); }
  private mutate(operation:(generation:number)=>Promise<void>):Promise<void> {
    const refresh=this.coordinator.settle();const previous=this.mutationQueue;
    this.coordinator.invalidate();const generation=this.generation();this.mutations++;
    // Wait for old cookie responses before sending another cookie mutation. Generation
    // checks alone cannot discard an HttpOnly Set-Cookie applied by the browser.
    const work=(async()=>{try{await previous;await refresh;this.current(generation);await operation(generation);}finally{this.mutations--;}})();
    this.mutationQueue=work.catch(()=>undefined);return work;
  }
  loseAuthentication(error:unknown):void {
    this.coordinator.invalidate();this.clear(error instanceof HttpErrorResponse && error.status === 401 ? 'anonymous' : 'unavailable');
  }
  async initialize():Promise<void> { try { await this.refresh(); } catch { /* refresh owns safe state */ } }
  refresh():Promise<string> {
    if(this.mutations) return Promise.reject(new Error('Authentication is changing.'));
    return this.coordinator.run(async () => {
      const generation=this.generation();
      try {
        const csrf=await this.requestToken(generation);
        const envelope=await this.transport.refresh(csrf);this.current(generation);this.validateEnvelope(envelope);
        const identity=await this.transport.me(envelope.accessToken);this.current(generation);this.validate(envelope,identity);
        this.token=envelope.accessToken;this.identity.set(identity);this.status.set('authenticated');this.ready.set(true);return this.token;
      } catch(error) {
        if(generation===this.generation()) this.clear(error instanceof HttpErrorResponse && error.status===401 ? 'anonymous':'unavailable');
        throw error;
      }
    });
  }
  async renewAfterExpiry(oldToken:string|null,generation:number):Promise<string> {
    this.current(generation);
    if(this.token && this.token !== oldToken) return this.token;
    const token=await this.refresh();this.current(generation);return token;
  }
  signIn(email?:string,password?:string):Promise<void> {return this.mutate(async generation=>{
    const previous={token:this.token,identity:this.identity(),status:this.status()};let issued=false;
    try {
      const csrf=await this.requestToken(generation);
      const envelope=await this.transport.signIn(email?.trim() ?? '',password ?? '',csrf);issued=true;this.current(generation);this.validateEnvelope(envelope);
      const identity=await this.transport.me(envelope.accessToken);this.current(generation);this.validate(envelope,identity);
      this.csrf=null;await this.requestToken(generation);this.current(generation);
      this.token=envelope.accessToken;this.identity.set(identity);this.status.set('authenticated');this.ready.set(true);
    } catch(error) {
      if(generation===this.generation()) {
        if(!issued && error instanceof HttpErrorResponse && (error.status===400 || error.status===401)) {this.token=previous.token;this.identity.set(previous.identity);this.status.set(previous.identity?previous.status:'anonymous');this.ready.set(true);}
        else this.clear('unavailable');
      }
      throw error;
    }
  });}
  signOut():Promise<void> {return this.mutate(async generation=>{
    const token=this.token;
    try {
      await this.transport.signOut(await this.requestToken(generation),token);this.current(generation);
      this.clear('anonymous');this.csrf=null;
      try { await this.requestToken(generation); } catch { if(generation===this.generation())this.status.set('unavailable'); }
    } catch(error) { if(generation===this.generation())this.clear('unavailable');throw error; }
  });}
}
