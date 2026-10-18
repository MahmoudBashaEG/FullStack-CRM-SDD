import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** One headline number (label + value + optional "Live" / "Period" scope chip). The value is passed as text, so formatting stays with the caller. */
@Component({
  selector: 'crm-kpi-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div class="head">
      @if (icon(); as name) {
        <span class="icon" aria-hidden="true"><mat-icon>{{ name }}</mat-icon></span>
      }
      <span class="label">{{ label() }}</span>
      @if (scope()) {
        <span class="scope">{{ scope() }}</span>
      }
    </div>
    <strong class="value">{{ value() }}</strong>
    <ng-content />
  `,
  styles: `
    :host {
      display: grid;
      gap: var(--crm-space-2);
      padding: var(--crm-space-4);
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--crm-radius-md);
      background: var(--mat-sys-surface);
      box-shadow: var(--crm-elevation-1);
      min-inline-size: 0;
    }
    .head {
      display: flex;
      align-items: center;
      gap: var(--crm-space-2);
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-large);
    }
    .icon {
      display: grid;
      place-items: center;
      inline-size: 32px;
      block-size: 32px;
      border-radius: var(--crm-radius-sm);
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
    }
    .icon mat-icon {
      font-size: 20px;
      inline-size: 20px;
      block-size: 20px;
    }
    .label {
      flex: 1 1 auto;
      overflow-wrap: anywhere;
    }
    .scope {
      padding-inline: var(--crm-space-2);
      border-radius: var(--crm-radius-pill);
      background: var(--mat-sys-surface-container-high);
      font: var(--mat-sys-label-small);
    }
    .value {
      font: var(--mat-sys-headline-medium);
      color: var(--mat-sys-on-surface);
      overflow-wrap: anywhere;
    }
  `,
})
export class KpiCard {
  readonly label = input.required<string>();
  readonly value = input.required<string>();
  readonly scope = input<string>();
  readonly icon = input<string>();
}
