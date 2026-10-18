import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { KpiCard } from '../../shared/ui/kpi-card/kpi-card';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { tabQuery } from '../../shared/ui/tab-query';
import { AgentTaskSummary } from '../tasks/task-models';
import { TasksApi } from '../tasks/tasks-api';
import { slaIndicatorTone } from '../sla/sla-models';
import { priorityTone, statusTone } from '../tickets/ticket-models';
import { AgentDashboardApi } from './agent-dashboard-api';
import { AgentDashboard } from './agent-dashboard-models';

@Component({
  selector: 'crm-agent-dashboard-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    LocalizedDatePipe,
    TranslatePipe,
    EmptyState,
    ErrorState,
    KpiCard,
    PageHeader,
    StatusBadge,
    MatButtonModule,
    MatChipsModule,
    MatIconModule,
    MatProgressBarModule,
    MatTabsModule,
  ],
  templateUrl: './agent-dashboard-page.html',
  styleUrl: './agent-dashboard-page.scss',
})
export class AgentDashboardPage {
  private readonly api = inject(AgentDashboardApi);
  private readonly tasksApi = inject(TasksApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly i18n = inject(I18n);
  private readonly requests = new Subject<void>();
  private readonly taskRequests = new Subject<void>();

  protected readonly priorityTone = priorityTone;
  protected readonly statusTone = statusTone;
  protected readonly tabs = tabQuery(['overview', 'tasks']);
  protected readonly slaIndicatorTone = slaIndicatorTone;

  protected readonly dashboard = signal<AgentDashboard | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  // Independent tasks section: it fails and retries on its own, so the ticket cards keep rendering.
  protected readonly taskSummary = signal<AgentTaskSummary | null>(null);
  protected readonly tasksLoading = signal(true);
  protected readonly tasksError = signal<string | null>(null);

  constructor() {
    this.requests
      .pipe(
        // A newer load cancels the older one. Errors are caught per request so the stream survives Retry.
        switchMap(() =>
          this.api.get().pipe(
            catchError((error: HttpErrorResponse) => {
              this.dashboard.set(null); // never show stale numbers or zeros after a failure
              this.error.set(describeHttpError(error, this.i18n.t('dashboard.loadError'), this.i18n.t));
              this.loading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(result => {
        this.dashboard.set(result);
        this.loading.set(false);
      });

    this.taskRequests
      .pipe(
        switchMap(() =>
          this.tasksApi.summary().pipe(
            catchError((error: HttpErrorResponse) => {
              this.taskSummary.set(null); // never show stale numbers or zeros after a failure
              this.tasksError.set(describeHttpError(error, this.i18n.t('dashboard.tasksLoadError'), this.i18n.t));
              this.tasksLoading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(result => {
        this.taskSummary.set(result);
        this.tasksLoading.set(false);
      });

    this.refresh();
  }

  protected refresh(): void {
    this.loading.set(true);
    this.error.set(null);
    this.requests.next();
    this.refreshTasks();
  }

  protected refreshTasks(): void {
    this.tasksLoading.set(true);
    this.tasksError.set(null);
    this.taskRequests.next();
  }
}
