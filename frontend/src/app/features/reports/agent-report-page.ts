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
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { ReportBar, ReportBars } from './report-bars';
import { formatUtcDay, parsePeriodParams, toPeriodQueryParams } from './report-format';
import { ReportPeriodFilter } from './report-period-filter';
import { ReportsApi } from './reports-api';
import { AgentPerformanceReport, DEFAULT_PRESET, ReportCount, ReportPeriodParams } from './reports-models';
import type { TicketPriority } from '../tickets/ticket-models';

@Component({
  selector: 'crm-agent-report-page',
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
  templateUrl: './agent-report-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './reports-page.scss'],
})
export class AgentReportPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(ReportsApi);
  private readonly reloads = new Subject<void>();

  protected readonly formatUtcDay = formatUtcDay;
  protected readonly period = signal<ReportPeriodParams>({ preset: DEFAULT_PRESET, from: null, to: null });
  protected readonly report = signal<AgentPerformanceReport | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly workloadBars = computed<ReportBar[]>(() =>
    (this.report()?.agents ?? []).map((a) => ({ label: a.name, value: a.activeAssigned })),
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

  /** "Urgent n · High n · Medium n · Low n" (highest priority first). */
  protected priorityMix(mix: ReportCount<TicketPriority>[]): string {
    return [...mix]
      .reverse()
      .map((c) => `${c.value} ${c.count}`)
      .join(' · ');
  }

  protected onPeriodChanged(p: ReportPeriodParams): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: toPeriodQueryParams(p), queryParamsHandling: 'merge' });
  }

  protected refresh(): void {
    this.reloads.next(); // manual Refresh / Retry: same period, new GET, no write
  }
}
