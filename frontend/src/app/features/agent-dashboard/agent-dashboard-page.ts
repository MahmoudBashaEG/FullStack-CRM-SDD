import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { AgentTaskSummary } from '../tasks/task-models';
import { TasksApi } from '../tasks/tasks-api';
import { slaIndicatorLabel, slaIndicatorTone } from '../sla/sla-models';
import { priorityTone, statusLabel, statusTone } from '../tickets/ticket-models';
import { AgentDashboardApi } from './agent-dashboard-api';
import { AgentDashboard } from './agent-dashboard-models';

@Component({
  selector: 'crm-agent-dashboard-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    DatePipe,
    PageHeader,
    StatusBadge,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './agent-dashboard-page.html',
  styleUrl: './agent-dashboard-page.scss',
})
export class AgentDashboardPage {
  private readonly api = inject(AgentDashboardApi);
  private readonly tasksApi = inject(TasksApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<void>();
  private readonly taskRequests = new Subject<void>();

  protected readonly priorityTone = priorityTone;
  protected readonly statusLabel = statusLabel;
  protected readonly statusTone = statusTone;
  protected readonly slaIndicatorLabel = slaIndicatorLabel;
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
              this.error.set(describeHttpError(error, 'Could not load your dashboard.'));
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
              this.tasksError.set(describeHttpError(error, 'Could not load your tasks.'));
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
