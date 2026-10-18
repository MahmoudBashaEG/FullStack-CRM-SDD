import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTimepickerModule } from '@angular/material/timepicker';
import { describeHttpError } from '../../core/api/describe-http-error';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { combineLocalDateTime, fromTimeOfDay, toDateOnly, toTimeOfDay } from '../../shared/date/date-only';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { formatUtcDay } from '../reports/report-format';
import { SlaApi } from './sla-api';
import { describeSaveError } from './sla-errors';
import { BusinessCalendar, Holiday, SlaDuePreview, WEEK_DAYS, WeekDay } from './sla-models';

function timeZoneIds(): string[] {
  try {
    const supported = (Intl as unknown as { supportedValuesOf?: (key: string) => string[] }).supportedValuesOf;
    return supported ? supported('timeZone') : [];
  } catch {
    return [];
  }
}

/** '09:00' (or '09:00:00') -> 'HH:mm:ss' for the API. */
function toApiTime(value: string): string {
  return value.length === 5 ? `${value}:00` : value;
}

/** SLA-001 capability 2: one global business calendar plus a deterministic due-date preview. */
@Component({
  selector: 'crm-business-calendar-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    LocalizedDatePipe,
    TranslatePipe,
    ErrorState,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatTimepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
  ],
  templateUrl: './business-calendar-form.html',
  styleUrl: './sla-admin.scss',
})
export class BusinessCalendarForm {
  protected readonly i18n = inject(I18n);
  private readonly api = inject(SlaApi);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly weekDays = WEEK_DAYS;
  protected readonly timeZones = timeZoneIds();
  protected readonly calendar = signal<BusinessCalendar | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  protected readonly form = this.fb.group({
    timeZoneId: ['Africa/Cairo', [Validators.required, Validators.maxLength(64)]],
    opensAt: [fromTimeOfDay('09:00') as Date | null, Validators.required],
    closesAt: [fromTimeOfDay('17:00') as Date | null, Validators.required],
  });
  protected readonly workingDays = signal<WeekDay[]>(['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday']);
  protected readonly holidays = signal<Holiday[]>([]);
  protected readonly holidayDate = new FormControl<Date | null>(null);
  protected readonly holidayName = new FormControl('', { nonNullable: true, validators: [Validators.maxLength(100)] });
  protected readonly sortedHolidays = computed(() => [...this.holidays()].sort((a, b) => a.date.localeCompare(b.date)));
  protected readonly clientError = computed(() => {
    const { opensAt: opens, closesAt: closes } = this.form.getRawValue();
    const opensAt = toTimeOfDay(opens);
    const closesAt = toTimeOfDay(closes);
    if (this.workingDays().length === 0) return this.i18n.t('sla.calendar.workingDayRequired');
    if (opensAt && closesAt && opensAt >= closesAt) return this.i18n.t('sla.calendar.closeAfterOpen');
    return null;
  });

  // Due-date preview
  protected readonly previewDay = new FormControl<Date | null>(null);
  protected readonly previewTime = new FormControl<Date | null>(null);
  protected readonly previewMinutes = new FormControl(240, { nonNullable: true, validators: [Validators.required, Validators.min(1)] });
  protected readonly preview = signal<SlaDuePreview | null>(null);
  protected readonly previewError = signal<string | null>(null);
  protected readonly previewing = signal(false);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.api
      .getCalendar()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: calendar => {
          this.show(calendar);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.loadError.set(describeHttpError(error, this.i18n.t('sla.calendar.loadFailed'), this.i18n.t));
          this.loading.set(false);
        },
      });
  }

  protected readonly formatDay = (day: string) => formatUtcDay(day, this.i18n.locale());

  protected isWorkingDay(day: WeekDay): boolean {
    return this.workingDays().includes(day);
  }

  protected toggleDay(day: WeekDay, checked: boolean): void {
    this.workingDays.update(days => (checked ? [...new Set([...days, day])] : days.filter(d => d !== day)));
  }

  protected addHoliday(): void {
    const date = toDateOnly(this.holidayDate.value);
    const name = this.holidayName.value.trim();
    if (!date || !name || this.holidayName.invalid) {
      this.saveError.set(this.i18n.t('sla.calendar.holidayRequired'));
      return;
    }
    if (this.holidays().some(h => h.date === date)) {
      this.saveError.set(this.i18n.t('sla.calendar.holidayDuplicate'));
      return;
    }
    this.saveError.set(null);
    this.holidays.update(list => [...list, { date, name }]);
    this.holidayDate.reset(null);
    this.holidayName.reset('');
  }

  protected removeHoliday(date: string): void {
    this.holidays.update(list => list.filter(h => h.date !== date));
  }

  protected save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.clientError() || this.saving()) {
      return;
    }
    const value = this.form.getRawValue();
    this.saving.set(true);
    this.saveError.set(null);
    this.api
      .saveCalendar({
        timeZoneId: value.timeZoneId.trim(),
        workingDays: WEEK_DAYS.filter(d => this.workingDays().includes(d)),
        opensAt: toApiTime(toTimeOfDay(value.opensAt) ?? ''),
        closesAt: toApiTime(toTimeOfDay(value.closesAt) ?? ''),
        holidays: this.sortedHolidays(),
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: calendar => {
          this.show(calendar);
          this.saving.set(false);
          this.snackBar.open(this.i18n.t('sla.calendar.saved'), undefined, { duration: 3000 });
        },
        error: (error: HttpErrorResponse) => {
          this.saving.set(false);
          this.saveError.set(describeSaveError(error, this.i18n.t('sla.calendar.saveFailed'), this.i18n.t));
        },
      });
  }

  protected runPreview(): void {
    if (this.previewMinutes.invalid || this.previewing()) {
      return;
    }
    const day = this.previewDay.value;
    const time = this.previewTime.value;
    if (this.previewDay.invalid || this.previewTime.invalid || (day && !time) || (!day && time)) {
      this.previewError.set(this.i18n.t(day ? 'validation.timeRequired' : 'validation.dateRequired'));
      return;
    }
    const start = combineLocalDateTime(day, time); // both empty ⇒ null ⇒ start now
    this.previewing.set(true);
    this.previewError.set(null);
    this.api
      .previewDue(start, Number(this.previewMinutes.value))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: result => {
          this.preview.set(result);
          this.previewing.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.preview.set(null);
          this.previewing.set(false);
          this.previewError.set(
            error.status === 409
              ? this.i18n.t('sla.calendar.saveFirst')
              : describeSaveError(error, this.i18n.t('sla.calendar.previewFailed'), this.i18n.t),
          );
        },
      });
  }

  private show(calendar: BusinessCalendar): void {
    this.calendar.set(calendar);
    if (!calendar.isConfigured) {
      return; // keep the suggested defaults; nothing is saved until the SuperAdmin presses Save
    }
    this.form.reset({
      timeZoneId: calendar.timeZoneId ?? '',
      opensAt: fromTimeOfDay((calendar.opensAt ?? '09:00:00').slice(0, 5)),
      closesAt: fromTimeOfDay((calendar.closesAt ?? '17:00:00').slice(0, 5)),
    });
    this.workingDays.set(calendar.workingDays);
    this.holidays.set(calendar.holidays);
  }
}
