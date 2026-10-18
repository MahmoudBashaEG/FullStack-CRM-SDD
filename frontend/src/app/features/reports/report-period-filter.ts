import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal, untracked } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { PRESET_LABELS, REPORT_PRESETS, ReportPeriodParams, ReportPreset } from './reports-models';

/** The one period control of all five report screens. Emits the chosen period; the page writes it to the URL. */
@Component({
  selector: 'crm-report-period-filter',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatButtonToggleModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule],
  template: `
    <div class="toolbar">
      <mat-button-toggle-group
        aria-label="Report period"
        [value]="customOpen() ? 'Custom' : value().preset"
        (change)="selectPreset($event.value)"
      >
        @for (p of presets; track p) {
          <mat-button-toggle [value]="p">{{ labels[p] }}</mat-button-toggle>
        }
      </mat-button-toggle-group>
      <button mat-stroked-button type="button" (click)="refresh.emit()" [disabled]="loading()">
        <mat-icon>refresh</mat-icon> Refresh
      </button>
    </div>
    @if (customOpen()) {
      <form class="custom" [formGroup]="form" (ngSubmit)="applyCustom()">
        <mat-form-field>
          <mat-label>From (UTC)</mat-label>
          <input matInput type="date" formControlName="from" />
        </mat-form-field>
        <mat-form-field>
          <mat-label>To (UTC)</mat-label>
          <input matInput type="date" formControlName="to" />
        </mat-form-field>
        <button mat-flat-button type="submit">Apply</button>
      </form>
      @if (customError(); as message) {
        <p class="error" role="alert">{{ message }}</p>
      }
    }
  `,
  styles: `
    :host { display: block; }
    .toolbar { display: flex; flex-wrap: wrap; align-items: center; gap: var(--crm-space-3); }
    .custom { display: flex; flex-wrap: wrap; align-items: center; gap: var(--crm-space-3); margin-block-start: var(--crm-space-3); }
    .error { color: var(--mat-sys-error); margin-block: var(--crm-space-2) 0; }
  `,
})
export class ReportPeriodFilter {
  readonly value = input.required<ReportPeriodParams>();
  readonly loading = input(false);
  readonly changed = output<ReportPeriodParams>();
  readonly refresh = output<void>();

  protected readonly presets = REPORT_PRESETS;
  protected readonly labels = PRESET_LABELS;
  protected readonly form = inject(NonNullableFormBuilder).group({ from: [''], to: [''] });
  protected readonly customOpen = signal(false);
  protected readonly customError = signal<string | null>(null);

  constructor() {
    effect(() => {
      // Keep the form in sync with the URL (back/forward, links from the dashboard).
      const v = this.value();
      untracked(() => {
        this.customOpen.set(v.preset === 'Custom');
        this.form.setValue({ from: v.from ?? '', to: v.to ?? '' });
        this.customError.set(null);
      });
    });
  }

  protected selectPreset(preset: ReportPreset): void {
    if (preset === 'Custom') {
      this.customOpen.set(true); // wait for Apply
      return;
    }
    this.changed.emit({ preset, from: null, to: null });
  }

  protected applyCustom(): void {
    const { from, to } = this.form.getRawValue();
    if (!from || !to) {
      this.customError.set('Choose both a start and an end date.');
      return;
    }
    if (from > to) {
      this.customError.set('The start date must be on or before the end date.'); // yyyy-MM-dd sorts lexically
      return;
    }
    this.customError.set(null);
    this.changed.emit({ preset: 'Custom', from, to });
  }
}
