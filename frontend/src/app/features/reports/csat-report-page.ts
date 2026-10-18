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
import { StatusBadge, StatusTone } from '../../shared/ui/status-badge/status-badge';
import { ReportBar, ReportBars } from './report-bars';
import { formatAverage, formatPercent, formatUtcDay, parsePeriodParams, toPeriodQueryParams } from './report-format';
import { ReportPeriodFilter } from './report-period-filter';
import { ReportsApi } from './reports-api';
import { CustomerSatisfactionReport, DEFAULT_PRESET, ReportPeriodParams } from './reports-models';

@Component({
  selector: 'crm-csat-report-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    DatePipe,
    PageHeader,
    StatusBadge,
    ReportPeriodFilter,
    ReportBars,
    MatCardModule,
    MatButtonModule,
    MatProgressBarModule,
  ],
  templateUrl: './csat-report-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './reports-page.scss'],
})
export class CsatReportPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(ReportsApi);
  private readonly reloads = new Subject<void>();

  protected readonly formatUtcDay = formatUtcDay;
  protected readonly formatPercent = formatPercent;
  protected readonly formatAverage = formatAverage;
  protected readonly period = signal<ReportPeriodParams>({ preset: DEFAULT_PRESET, from: null, to: null });
  protected readonly report = signal<CustomerSatisfactionReport | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly distributionBars = computed<ReportBar[]>(() =>
    (this.report()?.distribution ?? []).map((c) => ({ label: `${c.value} ★`, value: c.count })),
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
          this.api.customerSatisfaction(p).pipe(
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

  protected ratingTone(rating: number): StatusTone {
    return rating >= 4 ? 'success' : rating <= 2 ? 'error' : 'neutral';
  }

  protected onPeriodChanged(p: ReportPeriodParams): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: toPeriodQueryParams(p), queryParamsHandling: 'merge' });
  }

  protected refresh(): void {
    this.reloads.next(); // manual Refresh / Retry: same period, new GET, no write
  }
}
