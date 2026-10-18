import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/describe-http-error';
import { I18n } from '../../core/i18n/i18n';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { ErrorState } from '../../shared/ui/error-state/error-state';
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
    EmptyState,
    ErrorState,
    TranslatePipe,
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
  private readonly i18n = inject(I18n);
  private readonly api = inject(AgentsApi);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly columns = ['name', 'email', 'phone', 'status', 'actions'];
  protected readonly agents = signal<Agent[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.load();
  }

  protected reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.load();
  }

  private load(): void {
    this.api
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: agents => {
          this.agents.set(agents);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.error.set(describeHttpError(error, this.i18n.t('agents.list.loadFailed'), this.i18n.t));
          this.loading.set(false);
        },
      });
  }
}
