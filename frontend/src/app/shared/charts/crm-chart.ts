import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, afterNextRender, effect, inject, input, viewChild } from '@angular/core';
import type { Chart, ChartConfiguration, ChartDataset } from 'chart.js';
import { Branding } from '../../core/branding/branding';
import { I18n } from '../../core/i18n/i18n';
import { Theme } from '../../core/preferences/theme';

export type CrmChartType = 'bar' | 'horizontalBar' | 'line' | 'doughnut';

export interface CrmChartSeries {
  label: string;
  /** null = a gap (e.g. an undefined compliance rate); the tooltip prints N/A, never 0 %. */
  data: (number | null)[];
}

let registered: Promise<typeof import('chart.js')> | null = null;

/** Chart.js is imported lazily (only report routes ever reach this file) and its parts are registered once, tree-shaken. */
function loadChartJs(): Promise<typeof import('chart.js')> {
  registered ??= import('chart.js').then((module) => {
    module.Chart.register(
      module.BarController, module.LineController, module.DoughnutController,
      module.BarElement, module.LineElement, module.PointElement, module.ArcElement,
      module.CategoryScale, module.LinearScale, module.Tooltip, module.Legend, module.Filler,
    );
    return module;
  });
  return registered;
}

/**
 * Read-only Chart.js wrapper (PLAT-001 D2, C4). The canvas is `role="img"` with a summary label; the numbers are always available in the
 * keyboard-accessible data table of <crm-chart-card>, which is the supported non-pointer equivalent. Individual chart points are
 * pointer/touch (tooltip) only; they are not keyboard-focusable. No handler navigates or writes.
 */
@Component({
  selector: 'crm-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="frame" [style.block-size.px]="height()"><canvas #canvas role="img" [attr.aria-label]="ariaLabel()"></canvas></div>`,
  styles: `
    :host {
      display: block;
    }
    .frame {
      position: relative;
      inline-size: 100%;
    }
  `,
})
export class CrmChart {
  readonly type = input.required<CrmChartType>();
  readonly labels = input.required<string[]>();
  readonly series = input.required<CrmChartSeries[]>();
  readonly ariaLabel = input.required<string>();
  /** Y axis 0–100 with % ticks; null points are gaps. */
  readonly percent = input(false);
  readonly stacked = input(false);
  readonly height = input(260);

  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private readonly i18n = inject(I18n);
  private readonly theme = inject(Theme);
  private readonly branding = inject(Branding);
  private readonly root = inject(DOCUMENT).documentElement;
  private readonly destroyRef = inject(DestroyRef);
  private chart: Chart | null = null;
  private ready = false;
  private destroyed = false;

  constructor() {
    afterNextRender(() => {
      void loadChartJs()
        .then((module) => {
          if (this.destroyed) {
            return;
          }
          this.chart = new module.Chart(this.canvas().nativeElement, this.config());
          this.ready = true;
        })
        .catch(() => {
          /* chunk failed to load: the data table below still shows every number */
        });
    });
    effect(() => {
      // Re-run on any input, language, theme or accent change.
      this.type(); this.labels(); this.series(); this.percent(); this.stacked(); this.i18n.language(); this.theme.version(); this.branding.accent();
      if (this.ready && this.chart) {
        this.apply(this.chart);
      }
    });
    this.destroyRef.onDestroy(() => {
      this.destroyed = true;
      this.chart?.destroy();
      this.chart = null;
    });
  }

  /**
   * Resolved color of a CSS token. The tokens are `light-dark(...)` values, which a canvas cannot parse (it would paint black), so the token is
   * applied to a probe element's `color` and the computed (already resolved) rgb value is read back.
   */
  private color(name: string, fallback: string): string {
    const probe = this.root.ownerDocument.createElement('span');
    probe.style.color = `var(${name}, ${fallback})`;
    probe.style.display = 'none';
    this.root.appendChild(probe);
    const resolved = getComputedStyle(probe).color;
    probe.remove();
    return resolved || fallback;
  }

  private palette(): string[] {
    return [
      this.color('--mat-sys-primary', '#6750a4'),
      this.color('--mat-sys-tertiary', '#7d5260'),
      this.color('--crm-success', '#1b6e3a'),
      this.color('--crm-warning', '#8a5300'),
      this.color('--mat-sys-error', '#b3261e'),
      this.color('--mat-sys-secondary', '#625b71'),
      this.color('--mat-sys-outline', '#79747e'),
    ];
  }

  private config(): ChartConfiguration {
    const chart = { type: 'bar', data: { labels: [], datasets: [] }, options: {} } as ChartConfiguration;
    this.fill(chart);
    return chart;
  }

  private apply(chart: Chart): void {
    const next = this.config();
    chart.data = next.data;
    chart.options = next.options ?? {};
    chart.update();
  }

  private fill(config: ChartConfiguration): void {
    const type = this.type();
    const rtl = this.i18n.direction() === 'rtl';
    const colors = this.palette();
    const text = this.color('--mat-sys-on-surface-variant', '#49454f');
    const grid = this.color('--mat-sys-outline-variant', '#cac4d0');
    const horizontal = type === 'horizontalBar';
    const doughnut = type === 'doughnut';
    const fontFamily = getComputedStyle(this.root).getPropertyValue('--mat-sys-body-medium-font').trim() || 'Inter, sans-serif';
    const reduceMotion = typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
    const na = this.i18n.t('charts.na');

    config.type = type === 'horizontalBar' ? 'bar' : type;
    const datasets = this.series().map((series, index) => {
      const color = colors[index % colors.length];
      const base: Record<string, unknown> = { label: series.label, data: series.data };
      if (doughnut) {
        base['backgroundColor'] = this.labels().map((_, i) => colors[i % colors.length]);
        base['borderColor'] = this.color('--mat-sys-surface', '#fff');
        base['borderWidth'] = 2;
      } else if (type === 'line') {
        Object.assign(base, { borderColor: color, backgroundColor: color, pointBackgroundColor: color, tension: 0.25, spanGaps: false, fill: false });
      } else {
        Object.assign(base, { backgroundColor: color, borderRadius: 4, maxBarThickness: 36 });
      }
      return base as unknown as ChartDataset;
    });
    config.data = { labels: this.labels(), datasets };

    const tickColor = { color: text, font: { family: fontFamily } };
    const percent = this.percent();
    config.options = {
      responsive: true,
      maintainAspectRatio: false,
      animation: reduceMotion ? false : undefined,
      indexAxis: horizontal ? 'y' : 'x',
      plugins: {
        legend: {
          display: doughnut || this.series().length > 1,
          rtl,
          textDirection: rtl ? 'rtl' : 'ltr',
          labels: { color: text, font: { family: fontFamily } },
        },
        tooltip: {
          rtl,
          textDirection: rtl ? 'rtl' : 'ltr',
          bodyFont: { family: fontFamily },
          titleFont: { family: fontFamily },
          callbacks: {
            label: (context) => {
              const value = context.raw as number | null;
              const prefix = context.dataset.label ? `${context.dataset.label}: ` : '';
              return value === null || value === undefined ? `${prefix}${na}` : `${prefix}${value}${percent ? '%' : ''}`;
            },
          },
        },
      },
      scales: doughnut
        ? {}
        : {
            x: {
              stacked: this.stacked(),
              reverse: horizontal && rtl, // horizontal bars grow from the inline start; time axes are never mirrored
              grid: { color: grid },
              ticks: { ...tickColor, precision: 0 },
            },
            y: {
              stacked: this.stacked(),
              beginAtZero: true,
              ...(percent ? { min: 0, max: 100 } : {}),
              position: horizontal ? undefined : rtl ? 'right' : 'left',
              grid: { color: grid },
              ticks: { ...tickColor, precision: 0, ...(percent ? { callback: (value: string | number) => `${value}%` } : {}) },
            },
          },
    } as ChartConfiguration['options'];
  }
}
