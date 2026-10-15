import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/describe-http-error';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { Agent } from './agent-models';
import { AgentsApi } from './agents-api';

@Component({
  selector: 'crm-agent-list-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    PageHeader,
    StatusBadge,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './agent-list-page.html',
  styleUrl: './agent-list-page.scss',
})
export class AgentListPage {
  private readonly api = inject(AgentsApi);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly columns = ['name', 'email', 'phone', 'status', 'actions'];
  protected readonly agents = signal<Agent[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.api
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: agents => {
          this.agents.set(agents);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.error.set(describeHttpError(error, 'Could not load agents.'));
          this.loading.set(false);
        },
      });
  }
}
