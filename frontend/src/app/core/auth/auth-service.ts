import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, firstValueFrom, map, of, shareReplay, switchMap, tap } from 'rxjs';
import { AuthApi } from './auth-api';
import { AuthSessionResponse, CurrentUser, LoginRequest, RegisterCustomerRequest } from './auth-models';

const CSRF_COOKIE = 'XSRF-TOKEN';

/** Session state. The access token lives only in this signal — never in localStorage/sessionStorage. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(AuthApi);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly session = signal<AuthSessionResponse | null>(null);
  private refreshInFlight: Observable<boolean> | null = null;

  readonly currentUser = computed(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly role = computed(() => this.session()?.user.role ?? null);

  accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  login(request: LoginRequest): Observable<CurrentUser> {
    return this.api.login(request).pipe(
      tap(session => this.session.set(session)),
      map(session => session.user),
    );
  }

  register(request: RegisterCustomerRequest): Observable<CurrentUser> {
    return this.api.register(request).pipe(
      switchMap(() => this.login({ email: request.email, password: request.password })),
    );
  }

  /** Single-flight: concurrent callers share one POST /auth/refresh. Emits false when the session is gone. */
  refresh(): Observable<boolean> {
    this.refreshInFlight ??= this.api.refresh().pipe(
      map(session => {
        this.session.set(session);
        return true;
      }),
      catchError(() => {
        this.session.set(null);
        return of(false);
      }),
      finalize(() => (this.refreshInFlight = null)),
      shareReplay(1),
    );
    return this.refreshInFlight;
  }

  /** App initializer: silently regain an access token after a reload. Never rejects. */
  restoreSession(): Promise<void> {
    const hasCsrfCookie = this.document.cookie.split('; ').some(c => c.startsWith(`${CSRF_COOKIE}=`));
    return hasCsrfCookie ? firstValueFrom(this.refresh()).then(() => undefined) : Promise.resolve();
  }

  updateCurrentUser(user: CurrentUser): void {
    this.session.update(session => (session ? { ...session, user } : session));
  }

  logout(): void {
    this.api.logout().pipe(catchError(() => of(null))).subscribe(() => this.endSession());
  }

  /** Drops the in-memory session and returns to sign-in. */
  endSession(): void {
    this.session.set(null);
    void this.router.navigateByUrl('/sign-in');
  }
}
