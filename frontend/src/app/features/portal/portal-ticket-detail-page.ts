import { Clipboard, ClipboardModule } from '@angular/cdk/clipboard';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, combineLatest, forkJoin, map, startWith, switchMap, tap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { statusTone } from '../tickets/ticket-models';
import { PortalApi } from './portal-api';
import { PortalTicket, PortalTimelineEntry } from './portal-models';
import { PortalTicketFeedback } from './portal-ticket-feedback';
import { describeTimelineEntry } from './portal-timeline';

@Component({
  selector: 'crm-portal-ticket-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    LocalizedDatePipe,
    TranslatePipe,
    ClipboardModule,
    ErrorState,
    PageHeader,
    StatusBadge,
    PortalTicketFeedback,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './portal-ticket-detail-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './portal-page.scss'],
})
export class PortalTicketDetailPage {
  private readonly api = inject(PortalApi);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly i18n = inject(I18n);
  private readonly clipboard = inject(Clipboard);
  private readonly retry = new Subject<void>();

  protected readonly ticket = signal<PortalTicket | null>(null);
  protected readonly timeline = signal<PortalTimelineEntry[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly describe = describeTimelineEntry;
  protected readonly statusTone = statusTone;

  constructor() {
    // Loads for the current :id. A changed :id or Retry re-runs the whole load, clears first (never show the previous ticket),
    // and switchMap cancels any request still in flight for the old id.
    combineLatest([inject(ActivatedRoute).paramMap.pipe(map(params => params.get('id') ?? '')), this.retry.pipe(startWith(undefined))])
      .pipe(
        tap(() => {
          this.ticket.set(null);
          this.timeline.set([]);
          this.loadError.set(null);
          this.loading.set(true);
        }),
        switchMap(([id]) =>
          forkJoin({ ticket: this.api.getTicket(id), timeline: this.api.timeline(id) }).pipe(
            catchError((error: HttpErrorResponse) => {
              this.loadError.set(this.describeLoadError(error));
              this.loading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe(({ ticket, timeline }) => {
        this.ticket.set(ticket);
        this.timeline.set(timeline);
        this.loading.set(false);
      });
  }

  protected reload(): void {
    this.retry.next();
  }

  protected onFeedbackSaved(updated: PortalTicket): void {
    this.ticket.set(updated);
  }

  protected copyReference(id: string): void {
    this.clipboard.copy(id);
    this.snackBar.open(this.i18n.t('portal.detail.copied'), undefined, { duration: 2000 });
  }

  // The 404 text is the same for a missing ticket and another customer's ticket.
  private describeLoadError(error: HttpErrorResponse): string {
    if (error.status === 404) {
      const detail = (error.error as { detail?: string } | null)?.detail;
      return detail === 'Customer profile not found.' ? this.i18n.t('portal.notLinked') : this.i18n.t('portal.detail.notFound');
    }
    return describeHttpError(error, this.i18n.t('portal.detail.loadError'), this.i18n.t);
  }
}
