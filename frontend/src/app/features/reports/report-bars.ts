import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { barPercent } from './report-format';

export interface ReportBar {
  label: string;
  value: number;
  /** Text printed next to the bar; defaults to the value. */
  display?: string;
}

/** Simple horizontal bars. The bars are decoration (aria-hidden): every number is also printed as text. */
@Component({
  selector: 'crm-report-bars',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (items().length === 0 || max() === 0) {
      <p class="hint">{{ emptyText() }}</p>
    }
    <ul class="bars" [attr.aria-label]="label()">
      @for (item of items(); track item.label) {
        <li>
          <span class="bar-label">{{ item.label }}</span>
          <span class="bar-track" aria-hidden="true"><span class="bar-fill" [style.inline-size.%]="percent(item.value)"></span></span>
          <span class="bar-value">{{ item.display ?? item.value }}</span>
        </li>
      }
    </ul>
  `,
  styles: `
    .bars { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--crm-space-2); }
    li { display: grid; grid-template-columns: minmax(6rem, 30%) 1fr auto; align-items: center; gap: var(--crm-space-3); }
    .bar-track { block-size: 12px; background: var(--mat-sys-surface-container-high); border-radius: var(--crm-radius-pill); }
    .bar-fill { display: block; block-size: 100%; background: var(--mat-sys-primary); border-radius: inherit; }
    .bar-value { font: var(--mat-sys-label-large); }
    .hint { margin: 0 0 var(--crm-space-2); color: var(--mat-sys-on-surface-variant); }
  `,
})
export class ReportBars {
  readonly items = input.required<ReportBar[]>();
  readonly label = input.required<string>();
  readonly emptyText = input('No data for this period.');
  protected readonly max = computed(() => Math.max(0, ...this.items().map((i) => i.value)));

  protected percent(value: number): number {
    return barPercent(value, this.max());
  }
}
