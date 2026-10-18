import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { catchError, exhaustMap, merge, of, switchMap, timer } from 'rxjs';
import { AuthService } from '../auth/auth-service';
import { TaskReminderCheck } from './notification-models';
import { NotificationsApi } from './notifications-api';
import { ReminderBadgeRefresh } from './reminder-badge-refresh';

/**
 * Agent-only in-app reminder indicator. The ONLY polling in the app: check on start/sign-in and every 60 s while an Agent
 * session exists (exhaustMap: never overlapping). The server issues each due reminder once; this just asks.
 * Failures never alert repeatedly: the badge hides and the next tick retries.
 */
export const REMINDER_POLL_MS = 60_000;

@Component({
  selector: 'crm-notification-bell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatButtonModule, MatIconModule, MatBadgeModule],
  template: `
    <a mat-icon-button routerLink="/notifications" [attr.aria-label]="label()">
      <mat-icon [matBadge]="unread() || null" matBadgeColor="warn" [matBadgeHidden]="!unread()" aria-hidden="false">notifications</mat-icon>
    </a>
  `,
})
export class NotificationBell {
  private readonly auth = inject(AuthService);
  private readonly api = inject(NotificationsApi);
  private readonly refresh = inject(ReminderBadgeRefresh);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);

  protected readonly unread = signal<number | null>(null);
  protected readonly label = computed(() => {
    const n = this.unread();
    return n ? `Task reminders, ${n} unread` : 'Task reminders';
  });

  constructor() {
    toObservable(this.auth.role)
      .pipe(
        switchMap(role =>
          role === 'Agent'
            ? merge(timer(0, REMINDER_POLL_MS), this.refresh.requests).pipe(
                exhaustMap(() => this.api.check().pipe(catchError(() => of(null)))),
              )
            : of(null),
        ),
        takeUntilDestroyed(),
      )
      .subscribe(result => this.onResult(result));
  }

  private onResult(result: TaskReminderCheck | null): void {
    this.unread.set(result?.unreadCount ?? null);
    if (result && result.issuedNow > 0) {
      const text = result.issuedNow === 1 ? 'You have 1 new task reminder.' : `You have ${result.issuedNow} new task reminders.`;
      this.snackBar
        .open(text, 'View', { duration: 8000 })
        .onAction()
        .subscribe(() => void this.router.navigate(['/notifications']));
    }
  }
}
