import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { KpiCard } from '../../shared/ui/kpi-card/kpi-card';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { SlaApi } from './sla-api';
import { AutomationOutcome, SlaKind, SlaOverview, SlaTargetView, formatBusinessMinutes, slaKindLabel } from './sla-models';

/** SLA-001 capability 9: warning / breached tickets and recent automation results. */
@Component({
  selector: 'crm-sla-overview',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizedDatePipe, TranslatePipe, ErrorState, EmptyState, KpiCard, RouterLink, StatusBadge, MatButtonModule, MatIconModule, MatProgressBarModule],
  templateUrl: './sla-overview.html',
  styleUrl: './sla-admin.scss',
})
export class SlaOverviewPanel {
  protected readonly i18n = inject(I18n);
  private readonly api = inject(SlaApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<void>();

  protected readonly kindLabel = (kind: SlaKind) => slaKindLabel(kind, this.i18n.t);
  protected readonly overview = signal<SlaOverview | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.requests
      .pipe(
        switchMap(() =>
          this.api.getOverview().pipe(
            catchError((error: HttpErrorResponse) => {
              this.error.set(describeHttpError(error, this.i18n.t('sla.overview.loadFailed'), this.i18n.t));
              this.loading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(overview => {
        this.overview.set(overview);
        this.loading.set(false);
      });
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.requests.next();
  }

  /** The target that explains the row: the breached one, otherwise the pending one with the least time left. */
  protected focus(sla: { firstResponse: SlaTargetView; resolution: SlaTargetView }, breached: boolean): SlaTargetView {
    const candidates = [sla.firstResponse, sla.resolution].filter(t => (breached ? t.state === 'Breached' : t.state === 'Pending'));
    return candidates.sort((a, b) => (a.businessMinutesRemaining ?? 0) - (b.businessMinutesRemaining ?? 0))[0] ?? sla.firstResponse;
  }

  protected timeText(target: SlaTargetView): string {
    const remaining = target.businessMinutesRemaining;
    if (remaining === null) return '';
    const t = this.i18n.t;
    return remaining >= 0
      ? t('sla.overview.remaining', { time: formatBusinessMinutes(remaining, t) })
      : t('sla.overview.overdueBy', { time: formatBusinessMinutes(-remaining, t) });
  }

  protected outcomeLabel(outcome: AutomationOutcome): string {
    return this.i18n.t(`sla.outcome.${outcome}`);
  }
}
