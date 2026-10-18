import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, Router } from '@angular/router';
import { EMPTY, Subject, catchError, map, merge, switchMap, tap } from 'rxjs';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { CrmChart } from '../../shared/charts/crm-chart';
import { ChartCard, ChartRow } from '../../shared/ui/chart-card/chart-card';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { KpiCard } from '../../shared/ui/kpi-card/kpi-card';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { describeSaveError } from '../sla/sla-errors';
import type { TicketPriority } from '../tickets/ticket-models';
import { barChartHeight, formatUtcDay, parsePeriodParams, toPeriodQueryParams } from './report-format';
import { ReportPeriodFilter } from './report-period-filter';
import { ReportsApi } from './reports-api';
import { AgentPerformanceReport, DEFAULT_PRESET, ReportCount, ReportPeriodParams } from './reports-models';
import { ReportsNav } from './reports-nav';

@Component({
  selector: 'crm-agent-report-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
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
  templateUrl: './agent-report-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './reports-page.scss'],
})
export class AgentReportPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(ReportsApi);
  private readonly reloads = new Subject<void>();
  protected readonly i18n = inject(I18n);

  protected readonly period = signal<ReportPeriodParams>({ preset: DEFAULT_PRESET, from: null, to: null });
  protected readonly report = signal<AgentPerformanceReport | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly workloadLabels = computed(() => (this.report()?.agents ?? []).map((a) => a.name));
  protected readonly workloadSeries = computed(() => {
    const agents = this.report()?.agents ?? [];
    return [
      { label: this.i18n.t('enums.ticketStatus.Open'), data: agents.map((a) => a.open) },
      { label: this.i18n.t('enums.ticketStatus.InProgress'), data: agents.map((a) => a.inProgress) },
    ];
  });
  protected readonly workloadEmpty = computed(() => (this.report()?.agents ?? []).every((a) => a.activeAssigned === 0));
  protected readonly workloadHeight = computed(() => barChartHeight(this.workloadLabels().length));
  protected readonly workloadRows = computed<ChartRow[]>(() =>
    (this.report()?.agents ?? []).map((a) => ({
      label: a.name,
      value: `${this.i18n.t('enums.ticketStatus.Open')} ${a.open} · ${this.i18n.t('enums.ticketStatus.InProgress')} ${a.inProgress}`,
    })),
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
          this.api.agents(p).pipe(
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

  /** "Urgent n · High n · Medium n · Low n" (highest priority first). */
  protected priorityMix(mix: ReportCount<TicketPriority>[]): string {
    return [...mix]
      .reverse()
      .map((c) => `${this.i18n.t('enums.ticketPriority.' + c.value)} ${c.count}`)
      .join(' · ');
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
