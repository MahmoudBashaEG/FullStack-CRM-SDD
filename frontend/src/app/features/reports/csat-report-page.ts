import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, map, merge, switchMap, tap } from 'rxjs';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { CrmChart } from '../../shared/charts/crm-chart';
import { ChartCard, ChartRow } from '../../shared/ui/chart-card/chart-card';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { KpiCard } from '../../shared/ui/kpi-card/kpi-card';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge, StatusTone } from '../../shared/ui/status-badge/status-badge';
import { describeSaveError } from '../sla/sla-errors';
import { formatAverage, formatPercent, formatUtcDay, parsePeriodParams, toPeriodQueryParams } from './report-format';
import { ReportPeriodFilter } from './report-period-filter';
import { ReportsApi } from './reports-api';
import { CustomerSatisfactionReport, DEFAULT_PRESET, ReportPeriodParams } from './reports-models';
import { ReportsNav } from './reports-nav';

@Component({
  selector: 'crm-csat-report-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    LocalizedDatePipe,
    TranslatePipe,
    PageHeader,
    StatusBadge,
    ReportsNav,
    ReportPeriodFilter,
    ChartCard,
    CrmChart,
    ErrorState,
    KpiCard,
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
  protected readonly i18n = inject(I18n);

  protected readonly period = signal<ReportPeriodParams>({ preset: DEFAULT_PRESET, from: null, to: null });
  protected readonly report = signal<CustomerSatisfactionReport | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly distLabels = computed(() => (this.report()?.distribution ?? []).map((c) => `${c.value} ★`));
  protected readonly distSeries = computed(() => [
    { label: this.i18n.t('reports.csat.distributionSeries'), data: (this.report()?.distribution ?? []).map((c) => c.count) },
  ]);
  protected readonly distRows = computed<ChartRow[]>(() =>
    (this.report()?.distribution ?? []).map((c) => ({ label: `${c.value} ★`, value: String(c.count) })),
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
              this.error.set(describeSaveError(error, this.i18n.t('reports.loadError'), this.i18n.t));
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

  protected percent(value: number | null): string {
    return formatPercent(value, this.i18n.t);
  }

  protected average(value: number | null): string {
    return formatAverage(value, this.i18n.t);
  }

  protected formatDay(day: string): string {
    return formatUtcDay(day, this.i18n.locale());
  }

  protected onPeriodChanged(p: ReportPeriodParams): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: toPeriodQueryParams(p), queryParamsHandling: 'merge' });
  }

  protected refresh(): void {
    this.reloads.next(); // manual Refresh / Retry: same period, new GET, no write
  }
}
