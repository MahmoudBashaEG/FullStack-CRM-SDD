import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, map, of, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { TaskReminder } from '../../core/notifications/notification-models';
import { NotificationsApi } from '../../core/notifications/notifications-api';
import { ReminderBadgeRefresh } from '../../core/notifications/reminder-badge-refresh';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';

const PAGE_SIZE = 20;

@Component({
  selector: 'crm-notifications-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, RouterLink, PageHeader, StatusBadge, MatButtonModule, MatIconModule, MatProgressBarModule, MatSlideToggleModule],
  templateUrl: './notifications-page.html',
  styleUrl: '../../shared/ui/productivity-page.scss',
})
export class NotificationsPage {
  private readonly api = inject(NotificationsApi);
  private readonly badge = inject(ReminderBadgeRefresh);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<{ page: number; unreadOnly: boolean }>();
  private nextPage = 1;

  protected readonly items = signal<TaskReminder[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly unreadOnly = signal(false);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly hasMore = computed(() => this.items().length < this.totalCount());

  constructor() {
    this.requests
      .pipe(
        // Page 1 first asks the server to issue anything due, then lists. Errors are per request so Retry works.
        switchMap(r =>
          (r.page === 1 ? this.api.check().pipe(catchError(() => of(null))) : of(null)).pipe(
            switchMap(() => this.api.list(r.page, PAGE_SIZE, r.unreadOnly)),
            map(result => ({ r, result })),
            catchError((error: HttpErrorResponse) => {
              this.error.set(describeHttpError(error, 'Could not load reminders.'));
              this.loading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(({ r, result }) => {
        if (r.page === 1) {
          this.items.set(result.items);
        } else {
          const known = new Set(this.items().map(i => i.id));
          this.items.update(current => [...current, ...result.items.filter(i => !known.has(i.id))]);
        }
        this.totalCount.set(result.totalCount);
        this.nextPage = r.page + 1;
        this.loading.set(false);
      });

    this.load(1);
  }

  protected toggleUnread(value: boolean): void {
    this.unreadOnly.set(value);
    this.load(1);
  }

  protected load(page: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.requests.next({ page, unreadOnly: this.unreadOnly() });
  }

  protected loadMore(): void {
    this.load(this.nextPage);
  }

  protected markRead(item: TaskReminder): void {
    this.api.markRead(item.id).subscribe({
      next: () => {
        this.items.update(list => list.map(i => (i.id === item.id ? { ...i, isRead: true, readAtUtc: new Date().toISOString() } : i)));
        this.badge.next();
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 404) {
          this.items.update(list => list.filter(i => i.id !== item.id));
          this.snackBar.open('This reminder moved with its task to another Agent.', 'OK', { duration: 6000 });
          this.badge.next();
        } else {
          this.error.set(describeHttpError(error, 'Could not mark the reminder as read.'));
        }
      },
    });
  }
}
