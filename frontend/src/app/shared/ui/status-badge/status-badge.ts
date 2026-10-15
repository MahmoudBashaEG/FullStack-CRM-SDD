import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type StatusTone = 'success' | 'error' | 'pending' | 'neutral';

@Component({
  selector: 'crm-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-tone]': 'tone()', role: 'status' },
  template: `<span class="dot" aria-hidden="true"></span>{{ label() }}`,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: var(--crm-space-2);
      padding-block: var(--crm-space-1);
      padding-inline: var(--crm-space-3);
      border-radius: var(--crm-radius-pill);
      font: var(--mat-sys-label-large);
    }
    :host([data-tone='success']) {
      background: var(--crm-success-container);
      color: var(--crm-success);
    }
    :host([data-tone='error']) {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    :host([data-tone='pending']) {
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }
    :host([data-tone='neutral']) {
      background: var(--mat-sys-surface-container-high);
      color: var(--mat-sys-on-surface-variant);
    }
    .dot {
      inline-size: 8px;
      block-size: 8px;
      border-radius: 50%;
      background: currentColor;
    }
  `,
})
export class StatusBadge {
  readonly label = input.required<string>();
  readonly tone = input<StatusTone>('neutral');
}
