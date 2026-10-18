import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/** Error text with a Retry button (replaces the repeated `<p role="alert">` + Retry pairs). */
@Component({
  selector: 'crm-error-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, TranslatePipe],
  template: `
    <mat-icon aria-hidden="true">error_outline</mat-icon>
    <p role="alert">{{ message() }}</p>
    @if (retryable()) {
      <button mat-stroked-button type="button" (click)="retry.emit()">{{ 'common.retry' | t }}</button>
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--crm-space-3);
      padding: var(--crm-space-3) var(--crm-space-4);
      border-radius: var(--crm-radius-md);
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    p {
      flex: 1 1 200px;
      margin: 0;
      overflow-wrap: anywhere;
    }
  `,
})
export class ErrorState {
  readonly message = input.required<string>();
  readonly retryable = input(true);
  readonly retry = output<void>();
}
