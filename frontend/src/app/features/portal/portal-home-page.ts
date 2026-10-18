import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/describe-http-error';
import { Branding } from '../../core/branding/branding';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { statusTone } from '../tickets/ticket-models';
import { PortalApi } from './portal-api';
import { PortalTicketSummary } from './portal-models';

const RECENT = 5;

@Component({
  selector: 'crm-portal-home-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, LocalizedDatePipe, TranslatePipe, EmptyState, ErrorState, PageHeader, StatusBadge, MatCardModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  templateUrl: './portal-home-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './portal-page.scss'],
})
export class PortalHomePage {
  private readonly api = inject(PortalApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly i18n = inject(I18n);

  protected readonly branding = inject(Branding);

  protected readonly recentCount = RECENT;
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
          this.error.set(error.status === 404 ? this.i18n.t('portal.notLinked') : describeHttpError(error, this.i18n.t('portal.home.loadError'), this.i18n.t));
          this.loading.set(false);
        },
      });
  }
}
