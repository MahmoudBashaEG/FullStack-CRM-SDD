import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
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
import { describeSaveError } from '../sla/sla-errors';
import { formatBucketLabel, formatPercent, formatUtcDay, parsePeriodParams, toPeriodQueryParams } from './report-format';
import { ReportPeriodFilter } from './report-period-filter';
import { ReportsApi } from './reports-api';
import { DEFAULT_PRESET, ReportPeriodParams, SlaReport, SlaTargetSummary } from './reports-models';
import { ReportsNav } from './reports-nav';

@Component({
  selector: 'crm-sla-report-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    LocalizedDatePipe,
    TranslatePipe,
    PageHeader,
    ReportsNav,
    ReportPeriodFilter,
    ChartCard,
    CrmChart,
    ErrorState,
    KpiCard,
    MatButtonModule,
    MatProgressBarModule,
  ],
  templateUrl: './sla-report-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './reports-page.scss'],
})
export class SlaReportPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(ReportsApi);
  private readonly reloads = new Subject<void>();
  protected readonly i18n = inject(I18n);

  protected readonly period = signal<ReportPeriodParams>({ preset: DEFAULT_PRESET, from: null, to: null });
  protected readonly report = signal<SlaReport | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly trendLabels = computed(() =>
    (this.report()?.trend ?? []).map((b) => formatBucketLabel(b.start, b.end, this.i18n.locale())),
  );
  /** null is kept: an undecided bucket is a gap in the line, never 0 %. */
  protected readonly trendSeries = computed(() => {
    const trend = this.report()?.trend ?? [];
    return [
      { label: this.i18n.t('reports.sla.firstResponse'), data: trend.map((b) => b.responseCompliancePercent) },
      { label: this.i18n.t('reports.sla.resolution'), data: trend.map((b) => b.resolutionCompliancePercent) },
    ];
  });
  protected readonly trendEmpty = computed(() => this.trendSeries().every((s) => s.data.every((v) => v === null)));
  protected readonly trendRows = computed<ChartRow[]>(() => {
    const t = this.i18n.t;
    const labels = this.trendLabels();
    return (this.report()?.trend ?? []).map((b, i) => ({
      label: labels[i],
      value: t('reports.sla.trendRow', {
        response: formatPercent(b.responseCompliancePercent, t),
        responseMet: b.responseMet,
        responseDecided: b.responseMet + b.responseBreached,
        resolution: formatPercent(b.resolutionCompliancePercent, t),
        resolutionMet: b.resolutionMet,
        resolutionDecided: b.resolutionMet + b.resolutionBreached,
      }),
    }));
  });

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
          this.api.sla(p).pipe(
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

  protected outcomeLabels(): string[] {
    const t = this.i18n.t;
    return [t('reports.sla.met'), t('reports.sla.breached'), t('reports.sla.pending'), t('reports.sla.notTracked')];
  }

  protected outcomeData(s: SlaTargetSummary): number[] {
    return [s.met, s.breached, s.pending, s.notApplicable];
  }

  protected outcomeRows(s: SlaTargetSummary): ChartRow[] {
    const labels = this.outcomeLabels();
    return this.outcomeData(s).map((n, i) => ({ label: labels[i], value: String(n) }));
  }

  protected breakdown(s: SlaTargetSummary): string {
    return this.i18n.t('reports.sla.breakdown', {
      met: s.met,
      breached: s.breached,
      pending: s.pending,
      notTracked: s.notApplicable,
      tracked: s.applicable,
      cohort: s.cohortTickets,
    });
  }

  protected percent(value: number | null): string {
    return formatPercent(value, this.i18n.t);
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
