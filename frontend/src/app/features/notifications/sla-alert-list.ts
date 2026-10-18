import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, map, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { SlaAlert } from '../../core/notifications/notification-models';
import { ReminderBadgeRefresh } from '../../core/notifications/reminder-badge-refresh';
import { SlaAlertsApi } from '../../core/notifications/sla-alerts-api';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';

const PAGE_SIZE = 20;

/** SLA alerts inbox section (SuperAdmin + Agent). The server returns only the caller's alerts for tickets they can currently access. */
@Component({
  selector: 'crm-sla-alert-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizedDatePipe, TranslatePipe, RouterLink, EmptyState, ErrorState, StatusBadge, MatButtonModule, MatIconModule, MatProgressBarModule, MatSlideToggleModule],
  templateUrl: './sla-alert-list.html',
  styleUrl: '../../shared/ui/productivity-page.scss',
})
export class SlaAlertList {
  private readonly api = inject(SlaAlertsApi);
  private readonly badge = inject(ReminderBadgeRefresh);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(I18n);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<{ page: number; unreadOnly: boolean }>();
  private nextPage = 1;

  protected readonly items = signal<SlaAlert[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly unreadOnly = signal(false);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly hasMore = computed(() => this.items().length < this.totalCount());

  constructor() {
    this.requests
      .pipe(
        switchMap(r =>
          this.api.list(r.page, PAGE_SIZE, r.unreadOnly).pipe(
            map(result => ({ r, result })),
            catchError((error: HttpErrorResponse) => {
              this.error.set(describeHttpError(error, this.i18n.t('notifications.sla.loadError'), this.i18n.t));
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

  protected markRead(item: SlaAlert): void {
    this.api.markRead(item.id).subscribe({
      next: () => {
        this.items.update(list => list.map(i => (i.id === item.id ? { ...i, isRead: true, readAtUtc: new Date().toISOString() } : i)));
        this.badge.next();
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 404) {
          this.items.update(list => list.filter(i => i.id !== item.id));
          this.snackBar.open(this.i18n.t('notifications.sla.moved'), this.i18n.t('notifications.ok'), { duration: 6000 });
          this.badge.next();
        } else {
          this.error.set(describeHttpError(error, this.i18n.t('notifications.sla.markError'), this.i18n.t));
        }
      },
    });
  }
}
