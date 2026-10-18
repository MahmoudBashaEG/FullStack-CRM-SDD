import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** The one h1 of a page. Title/subtitle are strings (callers pass translated text); an `[actions]` slot sits at the inline end. */
@Component({
  selector: 'crm-page-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <header class="page-header">
      @if (icon(); as name) {
        <span class="icon" aria-hidden="true"><mat-icon>{{ name }}</mat-icon></span>
      }
      <div class="text">
        <h1>{{ title() }}</h1>
        @if (subtitle()) {
          <p>{{ subtitle() }}</p>
        }
      </div>
      <div class="actions"><ng-content select="[actions]" /></div>
    </header>
  `,
  styles: `
    .page-header {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--crm-space-3) var(--crm-space-4);
      margin-block-end: var(--crm-space-6);
    }
    .icon {
      display: grid;
      place-items: center;
      inline-size: 48px;
      block-size: 48px;
      border-radius: var(--crm-radius-md);
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
    }
    .text {
      flex: 1 1 240px;
      min-inline-size: 0;
    }
    h1 {
      font: var(--mat-sys-headline-medium);
      margin-block: 0 var(--crm-space-1);
      overflow-wrap: anywhere;
    }
    p {
      font: var(--mat-sys-body-large);
      color: var(--mat-sys-on-surface-variant);
      margin: 0;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--crm-space-2);
    }
    .actions:empty {
      display: none;
    }
  `,
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
  readonly icon = input<string>();
}
