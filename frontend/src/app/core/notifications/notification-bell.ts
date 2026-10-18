import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { catchError, exhaustMap, forkJoin, merge, of, switchMap, timer } from 'rxjs';
import { AuthService } from '../auth/auth-service';
import { SlaAlertCount, TaskReminderCheck } from './notification-models';
import { NotificationsApi } from './notifications-api';
import { ReminderBadgeRefresh } from './reminder-badge-refresh';
import { SlaAlertsApi } from './sla-alerts-api';

/**
 * In-app indicator for all staff (SLA-001 widened it from Agents only). The ONLY polling in the app: check on start/sign-in and every
 * 60 s while a staff session exists (exhaustMap: never overlapping). Agents: task reminders (the server issues each due reminder once)
 * plus SLA alerts; SuperAdmin: SLA alerts only (the reminder endpoint is Agent-only).
 * Failures never alert repeatedly: the badge hides and the next tick retries.
 */
export const REMINDER_POLL_MS = 60_000;

interface PollResult {
  reminders: TaskReminderCheck | null;
  sla: SlaAlertCount | null;
}

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
  private readonly slaAlerts = inject(SlaAlertsApi);
  private readonly refresh = inject(ReminderBadgeRefresh);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);
  private previousSlaUnread: number | null = null;

  protected readonly unread = signal<number | null>(null);
  protected readonly label = computed(() => {
    const n = this.unread();
    return n ? `Notifications, ${n} unread` : 'Notifications';
  });

  constructor() {
    toObservable(this.auth.role)
      .pipe(
        switchMap(role => {
          this.previousSlaUnread = null; // a different user/role never compares against the previous session's count
          if (role !== 'Agent' && role !== 'SuperAdmin') {
            return of(null);
          }
          return merge(timer(0, REMINDER_POLL_MS), this.refresh.requests).pipe(
            exhaustMap(() =>
              forkJoin({
                reminders: role === 'Agent' ? this.api.check().pipe(catchError(() => of(null))) : of(null),
                sla: this.slaAlerts.unreadCount().pipe(catchError(() => of(null))),
              }),
            ),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe(result => this.onResult(result));
  }

  private onResult(result: PollResult | null): void {
    const reminders = result?.reminders ?? null;
    const sla = result?.sla ?? null;
    this.unread.set(reminders || sla ? (reminders?.unreadCount ?? 0) + (sla?.unreadCount ?? 0) : null);

    if (reminders && reminders.issuedNow > 0) {
      const text = reminders.issuedNow === 1 ? 'You have 1 new task reminder.' : `You have ${reminders.issuedNow} new task reminders.`;
      this.notify(text);
    }
    if (sla) {
      if (this.previousSlaUnread !== null && sla.unreadCount > this.previousSlaUnread) {
        this.notify('You have new SLA alerts.');
      }
      this.previousSlaUnread = sla.unreadCount;
    }
  }

  private notify(text: string): void {
    this.snackBar
      .open(text, 'View', { duration: 8000 })
      .onAction()
      .subscribe(() => void this.router.navigate(['/notifications']));
  }
}
