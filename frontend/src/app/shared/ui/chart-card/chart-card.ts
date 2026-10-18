import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { EmptyState } from '../empty-state/empty-state';
import { ErrorState } from '../error-state/error-state';

export interface ChartRow {
  label: string;
  value: string;
}

/**
 * Card around a chart: title, optional summary, the projected <crm-chart>, and ALWAYS the numbers as a data table (visible when short,
 * a keyboard-operable <details> when long). Empty ⇒ an empty state instead of a chart of zeros; error ⇒ the error state with Retry.
 */
@Component({
  selector: 'crm-chart-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, TranslatePipe, EmptyState, ErrorState],
  template: `
    <section class="card" [attr.aria-labelledby]="titleId">
      <header>
        <h2 [id]="titleId">{{ title() }}</h2>
        @if (summary()) {
          <span class="summary">{{ summary() }}</span>
        }
      </header>
      @if (error(); as message) {
        <crm-error-state [message]="message" [retryable]="retryable()" (retry)="retry.emit()" />
      } @else if (empty()) {
        <crm-empty-state icon="insert_chart_outlined" [message]="emptyText() || ('charts.noData' | t)" />
      } @else {
        <ng-content />
        @if (rows().length > 0) {
          @if (rows().length <= 8) {
            <ng-container *ngTemplateOutlet="table" />
          } @else {
            <details>
              <summary>{{ 'charts.showData' | t }}</summary>
              <ng-container *ngTemplateOutlet="table" />
            </details>
          }
        }
      }
    </section>

    <ng-template #table>
      <div class="table-scroll">
        <table>
          <caption class="visually-hidden">{{ title() }}</caption>
          <thead>
            <tr>
              <th scope="col">{{ 'charts.category' | t }}</th>
              <th scope="col">{{ 'charts.value' | t }}</th>
            </tr>
          </thead>
          <tbody>
            @for (row of rows(); track $index) {
              <tr>
                <th scope="row"><bdi>{{ row.label }}</bdi></th>
                <td>{{ row.value }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </ng-template>
  `,
  styles: `
    :host {
      display: block;
    }
    .card {
      display: grid;
      gap: var(--crm-space-3);
      padding: var(--crm-space-4);
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--crm-radius-md);
      background: var(--mat-sys-surface);
      box-shadow: var(--crm-elevation-1);
      min-inline-size: 0;
    }
    header {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      justify-content: space-between;
      gap: var(--crm-space-2);
    }
    h2 {
      margin: 0;
      font: var(--mat-sys-title-medium);
    }
    .summary {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-large);
    }
    summary {
      cursor: pointer;
      padding-block: var(--crm-space-2);
      color: var(--mat-sys-primary);
      font: var(--mat-sys-label-large);
    }
    table {
      border-collapse: collapse;
    }
    th[scope='row'] {
      font-weight: 400;
    }
    td {
      text-align: end;
      font-variant-numeric: tabular-nums;
    }
    .visually-hidden {
      position: absolute;
      inline-size: 1px;
      block-size: 1px;
      overflow: hidden;
      clip-path: inset(50%);
    }
  `,
})
export class ChartCard {
  private static nextId = 0;
  protected readonly titleId = `crm-chart-card-${ChartCard.nextId++}`;

  readonly title = input.required<string>();
  readonly summary = input<string>();
  readonly empty = input(false);
  readonly emptyText = input<string>();
  readonly error = input<string | null>(null);
  readonly retryable = input(true);
  readonly rows = input<ChartRow[]>([]);
  readonly retry = output<void>();
}
