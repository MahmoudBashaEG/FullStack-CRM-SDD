import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/describe-http-error';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { statusLabel, statusTone } from '../tickets/ticket-models';
import { PortalApi } from './portal-api';
import { NOT_LINKED, PortalTicketSummary } from './portal-models';

const RECENT = 5;

@Component({
  selector: 'crm-portal-home-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, PageHeader, StatusBadge, MatCardModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  templateUrl: './portal-home-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './portal-page.scss'],
})
export class PortalHomePage {
  private readonly api = inject(PortalApi);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly recentCount = RECENT;
  protected readonly statusLabel = statusLabel;
  protected readonly statusTone = statusTone;
  protected readonly recent = signal<PortalTicketSummary[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api
      .listTickets({ page: 1, pageSize: RECENT, search: null, status: null })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: result => {
          this.recent.set(result.items);
          this.totalCount.set(result.totalCount);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.error.set(error.status === 404 ? NOT_LINKED : describeHttpError(error, 'Could not load your requests.'));
          this.loading.set(false);
        },
      });
  }
}
