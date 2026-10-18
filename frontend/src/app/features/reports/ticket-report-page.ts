import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, map, merge, switchMap, tap } from 'rxjs';
import { describeSaveError } from '../sla/sla-errors';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { statusLabel } from '../tickets/ticket-models';
import { ReportBar, ReportBars } from './report-bars';
import { formatBucketLabel, formatUtcDay, parsePeriodParams, toPeriodQueryParams } from './report-format';
import { ReportPeriodFilter } from './report-period-filter';
import { ReportsApi } from './reports-api';
import { DEFAULT_PRESET, ReportPeriodParams, TicketReport } from './reports-models';

@Component({
  selector: 'crm-ticket-report-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, PageHeader, ReportPeriodFilter, ReportBars, MatCardModule, MatButtonModule, MatProgressBarModule],
  templateUrl: './ticket-report-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './reports-page.scss'],
})
export class TicketReportPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(ReportsApi);
  private readonly reloads = new Subject<void>();

  protected readonly formatUtcDay = formatUtcDay;
  protected readonly period = signal<ReportPeriodParams>({ preset: DEFAULT_PRESET, from: null, to: null });
  protected readonly report = signal<TicketReport | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly statusBars = computed<ReportBar[]>(() =>
    (this.report()?.byStatus ?? []).map((c) => ({ label: statusLabel(c.value), value: c.count })),
  );
  protected readonly categoryBars = computed<ReportBar[]>(() =>
    (this.report()?.byCategory ?? []).map((c) => ({ label: c.value, value: c.count })),
  );
  protected readonly priorityBars = computed<ReportBar[]>(() =>
    (this.report()?.byPriority ?? []).map((c) => ({ label: c.value, value: c.count })),
  );
  protected readonly trendBars = computed<ReportBar[]>(() =>
    (this.report()?.trend ?? []).map((t) => ({ label: formatBucketLabel(t.start, t.end), value: t.count })),
  );

  constructor() {
    merge(
      // A period change hides the previous period's numbers until the new ones arrive (no stale metrics under a new label).
      this.route.queryParamMap.pipe(
        map(parsePeriodParams),
        tap((p) => {
          this.period.set(p);
          this.report.set(null);
        }),
      ),
      this.reloads.pipe(map(() => this.period())),
    )
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.error.set(null);
        }),
        switchMap((p) =>
          this.api.tickets(p).pipe(
            catchError((error: HttpErrorResponse) => {
              this.report.set(null); // never show stale numbers after a failure
              this.error.set(describeSaveError(error, 'Could not load the report.'));
              this.loading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((r) => {
        this.report.set(r);
        this.loading.set(false);
      });
  }

  protected onPeriodChanged(p: ReportPeriodParams): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: toPeriodQueryParams(p), queryParamsHandling: 'merge' });
  }

  protected refresh(): void {
    this.reloads.next(); // manual Refresh / Retry: same period, new GET, no write
  }
}
