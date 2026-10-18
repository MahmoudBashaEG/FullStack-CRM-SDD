import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { statusLabel } from '../tickets/ticket-models';
import { SlaApi } from './sla-api';
import { AutomationOutcome, SlaOverview, SlaTargetView, formatBusinessMinutes, slaKindLabel } from './sla-models';

/** SLA-001 capability 9: warning / breached tickets and recent automation results. */
@Component({
  selector: 'crm-sla-overview',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, RouterLink, StatusBadge, MatButtonModule, MatIconModule, MatProgressBarModule],
  templateUrl: './sla-overview.html',
  styleUrl: './sla-admin.scss',
})
export class SlaOverviewPanel {
  private readonly api = inject(SlaApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<void>();

  protected readonly statusLabel = statusLabel;
  protected readonly kindLabel = slaKindLabel;
  protected readonly overview = signal<SlaOverview | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.requests
      .pipe(
        switchMap(() =>
          this.api.getOverview().pipe(
            catchError((error: HttpErrorResponse) => {
              this.error.set(describeHttpError(error, 'Could not load the SLA overview.'));
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
    return remaining >= 0
      ? `${formatBusinessMinutes(remaining)} remaining`
      : `overdue by ${formatBusinessMinutes(-remaining)}`;
  }

  protected outcomeLabel(outcome: AutomationOutcome): string {
    switch (outcome) {
      case 'AlreadyEscalated':
        return 'Already escalated';
      case 'NotEscalatable':
        return 'Not escalatable';
      default:
        return outcome;
    }
  }
}
