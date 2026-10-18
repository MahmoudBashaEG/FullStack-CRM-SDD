import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, Subject, catchError, forkJoin, map, merge, of, switchMap, tap } from 'rxjs';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { CrmChart } from '../../shared/charts/crm-chart';
import { ChartCard, ChartRow } from '../../shared/ui/chart-card/chart-card';
import { KpiCard } from '../../shared/ui/kpi-card/kpi-card';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { describeSaveError } from '../sla/sla-errors';
import { barChartHeight, formatAverage, formatBucketLabel, formatPercent, formatUtcDay, parsePeriodParams, toPeriodQueryParams } from './report-format';
import { ReportPeriodFilter } from './report-period-filter';
import { ReportsApi } from './reports-api';
import {
  AgentPerformanceReport,
  CustomerSatisfactionReport,
  DEFAULT_PRESET,
  ReportPeriod,
  ReportPeriodParams,
  SlaReport,
  TicketReport,
} from './reports-models';
import { ReportsNav } from './reports-nav';

interface Section<T> {
  data: T | null;
  error: string | null;
}

interface Overview {
  tickets: Section<TicketReport>;
  sla: Section<SlaReport>;
  agents: Section<AgentPerformanceReport>;
  csat: Section<CustomerSatisfactionReport>;
}

const section = <T>(source: Observable<T>, fallback: string, t: I18n['t']): Observable<Section<T>> =>
  source.pipe(
    map((data) => ({ data, error: null })),
    catchError((e: HttpErrorResponse) => of({ data: null, error: describeSaveError(e, fallback, t) })),
  );

/**
 * RM6: composes the SAME four endpoints the report pages use, for one shared period. No dashboard-specific numbers (R9).
 * Each section keeps its own error + Retry so one failing endpoint does not hide the others.
 */
@Component({
  selector: 'crm-management-dashboard-page',
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
    KpiCard,
    MatButtonModule,
    MatProgressBarModule,
  ],
  templateUrl: './management-dashboard-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './reports-page.scss'],
})
export class ManagementDashboardPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(ReportsApi);
  private readonly reloads = new Subject<void>();
  protected readonly i18n = inject(I18n);

  protected readonly period = signal<ReportPeriodParams>({ preset: DEFAULT_PRESET, from: null, to: null });
  protected readonly overview = signal<Overview | null>(null);
  protected readonly loading = signal(true);

  /** The resolved period, from whichever section answered first (all four resolve the same input). */
  protected readonly resolvedPeriod = computed<ReportPeriod | null>(() => {
    const o = this.overview();
    return (o?.tickets.data ?? o?.sla.data ?? o?.agents.data ?? o?.csat.data)?.period ?? null;
  });
  protected readonly generatedAtUtc = computed(() => {
    const o = this.overview();
    return (o?.tickets.data ?? o?.sla.data ?? o?.agents.data ?? o?.csat.data)?.generatedAtUtc ?? null;
  });

  protected readonly trendLabels = computed(() =>
    (this.overview()?.tickets.data?.trend ?? []).map((t) => formatBucketLabel(t.start, t.end, this.i18n.locale())),
  );
  protected readonly trendSeries = computed(() => [
    { label: this.i18n.t('reports.ticket.trendSeries'), data: (this.overview()?.tickets.data?.trend ?? []).map((t) => t.count) },
  ]);
  protected readonly trendRows = computed<ChartRow[]>(() => {
    const labels = this.trendLabels();
    return (this.overview()?.tickets.data?.trend ?? []).map((t, i) => ({ label: labels[i], value: String(t.count) }));
  });

  protected readonly workloadLabels = computed(() => (this.overview()?.agents.data?.agents ?? []).map((a) => a.name));
  protected readonly workloadSeries = computed(() => [
    { label: this.i18n.t('reports.dashboard.workloadAria'), data: (this.overview()?.agents.data?.agents ?? []).map((a) => a.activeAssigned) },
  ]);
  protected readonly workloadHeight = computed(() => barChartHeight(this.workloadLabels().length));
  protected readonly workloadEmpty = computed(() => (this.overview()?.agents.data?.agents ?? []).every((a) => a.activeAssigned === 0));
  protected readonly workloadRows = computed<ChartRow[]>(() =>
    (this.overview()?.agents.data?.agents ?? []).map((a) => ({ label: a.name, value: String(a.activeAssigned) })),
  );

  protected readonly ratingLabels = computed(() => (this.overview()?.csat.data?.distribution ?? []).map((c) => `${c.value} ★`));
  protected readonly ratingSeries = computed(() => [
    { label: this.i18n.t('reports.csat.distributionSeries'), data: (this.overview()?.csat.data?.distribution ?? []).map((c) => c.count) },
  ]);
  protected readonly ratingRows = computed<ChartRow[]>(() =>
    (this.overview()?.csat.data?.distribution ?? []).map((c) => ({ label: `${c.value} ★`, value: String(c.count) })),
  );

  constructor() {
    merge(
      // A period change hides the previous period's numbers until the new ones arrive (no stale metrics under a new label).
      this.route.queryParamMap.pipe(
        map(parsePeriodParams),
        tap((p) => {
          this.period.set(p);
          this.overview.set(null);
        }),
      ),
      this.reloads.pipe(map(() => this.period())),
    )
      .pipe(
        tap(() => this.loading.set(true)),
        switchMap((p) => {
          const t = this.i18n.t;
          return forkJoin({
            tickets: section(this.api.tickets(p), t('reports.dashboard.ticketsError'), t),
            sla: section(this.api.sla(p), t('reports.dashboard.slaError'), t),
            agents: section(this.api.agents(p), t('reports.dashboard.agentsError'), t),
            csat: section(this.api.customerSatisfaction(p), t('reports.dashboard.csatError'), t),
          });
        }),
        takeUntilDestroyed(),
      )
      .subscribe((o) => {
        this.overview.set(o);
        this.loading.set(false);
      });
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
    this.reloads.next(); // manual Refresh / Retry: four new GETs, no write, no polling
  }
}
