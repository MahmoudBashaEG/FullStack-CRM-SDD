import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { I18n } from '../../core/i18n/i18n';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { fromDateOnly, toDateOnly } from '../../shared/date/date-only';
import { PRESET_LABEL_KEYS, REPORT_PRESETS, ReportPeriodParams, ReportPreset } from './reports-models';

/** The one period control of all five report screens. Emits the chosen period; the page writes it to the URL. */
@Component({
  selector: 'crm-report-period-filter',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatButtonToggleModule, MatFormFieldModule, MatDatepickerModule, MatButtonModule, MatIconModule, TranslatePipe],
  template: `
    <div class="toolbar">
      <mat-button-toggle-group
        class="presets"
        [attr.aria-label]="'reports.preset.label' | t"
        [value]="customOpen() ? 'Custom' : value().preset"
        (change)="selectPreset($event.value)"
      >
        @for (p of presets; track p) {
          <mat-button-toggle [value]="p">{{ labels[p] | t }}</mat-button-toggle>
        }
      </mat-button-toggle-group>
      <button mat-stroked-button type="button" (click)="refresh.emit()" [disabled]="loading()">
        <mat-icon>refresh</mat-icon> {{ 'common.refresh' | t }}
      </button>
    </div>
    @if (customOpen()) {
      <form class="custom" [formGroup]="form" (ngSubmit)="applyCustom()">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'reports.customRange' | t }}</mat-label>
          <mat-date-range-input [rangePicker]="picker">
            <input matStartDate formControlName="from" [placeholder]="'reports.from' | t" />
            <input matEndDate formControlName="to" [placeholder]="'reports.to' | t" />
          </mat-date-range-input>
          <mat-hint>{{ 'reports.utcDaysHint' | t }} · {{ 'dates.inputFormatHint' | t }}</mat-hint>
          <mat-datepicker-toggle matIconSuffix [for]="picker" />
          <mat-date-range-picker #picker />
          @if (form.controls.from.hasError('matDatepickerParse') || form.controls.to.hasError('matDatepickerParse')) {
            <mat-error>{{ 'validation.invalidDate' | t }}</mat-error>
          }
        </mat-form-field>
        <button mat-flat-button type="submit">{{ 'common.apply' | t }}</button>
      </form>
      @if (customError(); as message) {
        <p class="error" role="alert">{{ message }}</p>
      }
    }
  `,
  styles: `
    :host { display: block; }
    .toolbar { display: flex; flex-wrap: wrap; align-items: center; gap: var(--crm-space-3); }
    .presets { max-inline-size: 100%; overflow-x: auto; }
    .custom { display: flex; flex-wrap: wrap; align-items: center; gap: var(--crm-space-3); margin-block-start: var(--crm-space-3); }
    .custom mat-form-field { flex: 1 1 280px; max-inline-size: 380px; }
    .error { color: var(--mat-sys-error); margin-block: var(--crm-space-2) 0; }
  `,
})
export class ReportPeriodFilter {
  private readonly i18n = inject(I18n);

  readonly value = input.required<ReportPeriodParams>();
  readonly loading = input(false);
  readonly changed = output<ReportPeriodParams>();
  readonly refresh = output<void>();

  protected readonly presets = REPORT_PRESETS;
  protected readonly labels = PRESET_LABEL_KEYS;
  protected readonly form = new FormGroup({
    from: new FormControl<Date | null>(null),
    to: new FormControl<Date | null>(null),
  });
  protected readonly customOpen = signal(false);
  protected readonly customError = signal<string | null>(null);

  constructor() {
    effect(() => {
      // Keep the form in sync with the URL (back/forward, links from the dashboard).
      const v = this.value();
      untracked(() => {
        this.customOpen.set(v.preset === 'Custom');
        this.form.setValue({ from: fromDateOnly(v.from), to: fromDateOnly(v.to) });
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
    const raw = this.form.getRawValue();
    const from = toDateOnly(raw.from);
    const to = toDateOnly(raw.to);
    if (!from || !to) {
      this.customError.set(this.i18n.t('reports.chooseBoth'));
      return;
    }
    if (from > to) {
      this.customError.set(this.i18n.t('reports.startBeforeEnd')); // yyyy-MM-dd sorts lexically
      return;
    }
    this.customError.set(null);
    this.changed.emit({ preset: 'Custom', from, to });
  }
}
