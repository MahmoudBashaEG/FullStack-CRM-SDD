import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, Subject, catchError, forkJoin, map, merge, of, switchMap, tap } from 'rxjs';
import { describeSaveError } from '../sla/sla-errors';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { ReportBar, ReportBars } from './report-bars';
import { formatAverage, formatBucketLabel, formatPercent, formatUtcDay, parsePeriodParams, toPeriodQueryParams } from './report-format';
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

const section = <T>(source: Observable<T>, fallback: string): Observable<Section<T>> =>
  source.pipe(
    map((data) => ({ data, error: null })),
    catchError((e: HttpErrorResponse) => of({ data: null, error: describeSaveError(e, fallback) })),
  );

/**
 * RM6: composes the SAME four endpoints the report pages use, for one shared period. No dashboard-specific numbers (R9).
 * Each section keeps its own error + Retry so one failing endpoint does not hide the others.
 */
@Component({
  selector: 'crm-management-dashboard-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, PageHeader, ReportPeriodFilter, ReportBars, MatCardModule, MatButtonModule, MatProgressBarModule],
  templateUrl: './management-dashboard-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './reports-page.scss'],
})
export class ManagementDashboardPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(ReportsApi);
  private readonly reloads = new Subject<void>();

  protected readonly formatUtcDay = formatUtcDay;
  protected readonly formatPercent = formatPercent;
  protected readonly formatAverage = formatAverage;
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

  protected readonly trendBars = computed<ReportBar[]>(() =>
    (this.overview()?.tickets.data?.trend ?? []).map((t) => ({ label: formatBucketLabel(t.start, t.end), value: t.count })),
  );
  protected readonly workloadBars = computed<ReportBar[]>(() =>
    (this.overview()?.agents.data?.agents ?? []).map((a) => ({ label: a.name, value: a.activeAssigned })),
  );
  protected readonly ratingBars = computed<ReportBar[]>(() =>
    (this.overview()?.csat.data?.distribution ?? []).map((c) => ({ label: `${c.value} ★`, value: c.count })),
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
        switchMap((p) =>
          forkJoin({
            tickets: section(this.api.tickets(p), 'Could not load ticket figures.'),
            sla: section(this.api.sla(p), 'Could not load SLA figures.'),
            agents: section(this.api.agents(p), 'Could not load Agent figures.'),
            csat: section(this.api.customerSatisfaction(p), 'Could not load satisfaction figures.'),
          }),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((o) => {
        this.overview.set(o);
        this.loading.set(false);
      });
  }

  protected onPeriodChanged(p: ReportPeriodParams): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: toPeriodQueryParams(p), queryParamsHandling: 'merge' });
  }

  protected refresh(): void {
    this.reloads.next(); // manual Refresh / Retry: four new GETs, no write, no polling
  }
}
