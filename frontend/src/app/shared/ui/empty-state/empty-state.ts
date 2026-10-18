import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** Shared empty state (no rows / no data). Text is passed in already translated; an optional action can be projected. */
@Component({
  selector: 'crm-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  host: { role: 'status' },
  template: `
    <mat-icon aria-hidden="true">{{ icon() }}</mat-icon>
    @if (title()) {
      <strong>{{ title() }}</strong>
    }
    @if (message()) {
      <span>{{ message() }}</span>
    }
    <ng-content />
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--crm-space-2);
      padding: var(--crm-space-6) var(--crm-space-4);
      text-align: center;
      color: var(--mat-sys-on-surface-variant);
    }
    mat-icon {
      inline-size: 40px;
      block-size: 40px;
      font-size: 40px;
      color: var(--mat-sys-outline);
    }
    strong {
      font: var(--mat-sys-title-medium);
      color: var(--mat-sys-on-surface);
    }
  `,
})
export class EmptyState {
  readonly icon = input('inbox');
  readonly title = input<string>();
  readonly message = input<string>();
}
