import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, debounceTime, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { AuthService } from '../../core/auth/auth-service';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { Agent } from '../agents/agent-models';
import { AgentsApi } from '../agents/agents-api';
import { AgentTask, AgentTaskStatus, TaskDueFilter, TaskListParams } from './task-models';
import { TasksApi } from './tasks-api';

@Component({
  selector: 'crm-task-list-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DatePipe,
    PageHeader,
    StatusBadge,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './task-list-page.html',
  styleUrl: '../../shared/ui/productivity-page.scss',
})
export class TaskListPage {
  private readonly api = inject(TasksApi);
  private readonly auth = inject(AuthService);
  private readonly agentsApi = inject(AgentsApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<TaskListParams>();

  protected readonly isSuperAdmin = computed(() => this.auth.role() === 'SuperAdmin'); // wording only; the API scopes the data
  protected readonly pageSizeOptions = [10, 20, 50, 100];
  protected readonly filters = inject(NonNullableFormBuilder).group({
    search: [''],
    status: ['Pending' as AgentTaskStatus | 'Any'],
    dueState: [null as TaskDueFilter | null],
    hasReminder: [null as boolean | null],
    archived: [false],
    assignedAgentUserId: [null as string | null],
  });
  protected readonly agents = signal<Agent[]>([]);
  protected readonly tasks = signal<AgentTask[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.requests
      .pipe(
        switchMap(params =>
          this.api.list(params).pipe(
            catchError((error: HttpErrorResponse) => {
              this.error.set(describeHttpError(error, 'Could not load tasks.'));
              this.loading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(result => {
        this.tasks.set(result.items);
        this.totalCount.set(result.totalCount);
        this.page.set(result.page);
        this.pageSize.set(result.pageSize);
        this.loading.set(false);
      });

    this.filters.valueChanges.pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load(1));

    if (this.auth.role() === 'SuperAdmin') {
      this.agentsApi.list().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: list => this.agents.set(list), error: () => this.agents.set([]) });
    }

    this.load(1);
  }

  protected hasFilters(): boolean {
    const f = this.filters.getRawValue();
    return !!(f.search.trim() || f.status !== 'Pending' || f.dueState || f.hasReminder !== null || f.archived || f.assignedAgentUserId);
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.load(event.pageIndex + 1);
  }

  protected reload(): void {
    this.load(this.page());
  }

  private load(page: number): void {
    const f = this.filters.getRawValue();
    this.loading.set(true);
    this.error.set(null);
    this.requests.next({
      page,
      pageSize: this.pageSize(),
      search: f.search,
      status: f.status === 'Any' ? null : f.status,
      dueState: f.dueState,
      archived: f.archived,
      hasReminder: f.hasReminder,
      assignedAgentUserId: f.assignedAgentUserId,
    });
  }
}
