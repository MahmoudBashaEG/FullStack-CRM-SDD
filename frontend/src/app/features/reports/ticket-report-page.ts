import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, Router } from '@angular/router';
import { EMPTY, Subject, catchError, map, merge, switchMap, tap } from 'rxjs';
import { describeSaveError } from '../sla/sla-errors';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ChartCard, ChartRow } from '../../shared/ui/chart-card/chart-card';
import { CrmChart } from '../../shared/charts/crm-chart';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { KpiCard } from '../../shared/ui/kpi-card/kpi-card';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { formatBucketLabel, formatUtcDay, parsePeriodParams, toPeriodQueryParams } from './report-format';
import { ReportPeriodFilter } from './report-period-filter';
import { ReportsNav } from './reports-nav';
import { ReportsApi } from './reports-api';
import { DEFAULT_PRESET, ReportPeriodParams, TicketReport } from './reports-models';

@Component({
  selector: 'crm-ticket-report-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizedDatePipe, TranslatePipe, PageHeader, ReportsNav, ReportPeriodFilter, ChartCard, CrmChart, ErrorState, KpiCard, MatProgressBarModule],
  templateUrl: './ticket-report-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './reports-page.scss'],
})
export class TicketReportPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(ReportsApi);
  protected readonly i18n = inject(I18n);
  private readonly reloads = new Subject<void>();

  protected formatDay(day: string): string {
    return formatUtcDay(day, this.i18n.locale());
  }
  protected readonly period = signal<ReportPeriodParams>({ preset: DEFAULT_PRESET, from: null, to: null });
  protected readonly report = signal<TicketReport | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  private readonly counts = <T extends string>(items: { value: T; count: number }[], prefix: string) => ({
    labels: items.map((c) => this.i18n.t(`${prefix}.${c.value}`)),
    data: items.map((c) => c.count),
  });
  private rows(labels: string[], data: number[]): ChartRow[] {
    return labels.map((label, i) => ({ label, value: String(data[i]) }));
  }

  protected readonly status = computed(() => this.counts(this.report()?.byStatus ?? [], 'enums.ticketStatus'));
  protected readonly category = computed(() => this.counts(this.report()?.byCategory ?? [], 'enums.ticketCategory'));
  protected readonly priority = computed(() => this.counts(this.report()?.byPriority ?? [], 'enums.ticketPriority'));
  protected readonly trend = computed(() => {
    const trend = this.report()?.trend ?? [];
    return {
      labels: trend.map((b) => formatBucketLabel(b.start, b.end, this.i18n.locale())),
      data: trend.map((b) => b.count),
    };
  });
  protected readonly statusRows = computed(() => this.rows(this.status().labels, this.status().data));
  protected readonly categoryRows = computed(() => this.rows(this.category().labels, this.category().data));
  protected readonly priorityRows = computed(() => this.rows(this.priority().labels, this.priority().data));
  protected readonly trendRows = computed(() => this.rows(this.trend().labels, this.trend().data));

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

  protected onPeriodChanged(p: ReportPeriodParams): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: toPeriodQueryParams(p), queryParamsHandling: 'merge' });
  }

  protected refresh(): void {
    this.reloads.next(); // manual Refresh / Retry: same period, new GET, no write
  }
}
