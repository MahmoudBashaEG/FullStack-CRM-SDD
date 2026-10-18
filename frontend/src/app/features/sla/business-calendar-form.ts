import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { describeHttpError } from '../../core/api/describe-http-error';
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
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
  ],
  templateUrl: './business-calendar-form.html',
  styleUrl: './sla-admin.scss',
})
export class BusinessCalendarForm {
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
    opensAt: ['09:00', Validators.required],
    closesAt: ['17:00', Validators.required],
  });
  protected readonly workingDays = signal<WeekDay[]>(['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday']);
  protected readonly holidays = signal<Holiday[]>([]);
  protected readonly holidayDate = new FormControl('', { nonNullable: true });
  protected readonly holidayName = new FormControl('', { nonNullable: true, validators: [Validators.maxLength(100)] });
  protected readonly sortedHolidays = computed(() => [...this.holidays()].sort((a, b) => a.date.localeCompare(b.date)));
  protected readonly clientError = computed(() => {
    const { opensAt, closesAt } = this.form.getRawValue();
    if (this.workingDays().length === 0) return 'Select at least one working day.';
    if (opensAt && closesAt && opensAt >= closesAt) return 'Closing time must be after opening time.';
    return null;
  });

  // Due-date preview
  protected readonly previewStart = new FormControl('', { nonNullable: true });
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
          this.loadError.set(describeHttpError(error, 'Could not load the business calendar.'));
          this.loading.set(false);
        },
      });
  }

  protected isWorkingDay(day: WeekDay): boolean {
    return this.workingDays().includes(day);
  }

  protected toggleDay(day: WeekDay, checked: boolean): void {
    this.workingDays.update(days => (checked ? [...new Set([...days, day])] : days.filter(d => d !== day)));
  }

  protected addHoliday(): void {
    const date = this.holidayDate.value;
    const name = this.holidayName.value.trim();
    if (!date || !name || this.holidayName.invalid) {
      this.saveError.set('Enter a date and a name for the holiday.');
      return;
    }
    if (this.holidays().some(h => h.date === date)) {
      this.saveError.set('That holiday date is already in the list.');
      return;
    }
    this.saveError.set(null);
    this.holidays.update(list => [...list, { date, name }]);
    this.holidayDate.reset('');
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
        opensAt: toApiTime(value.opensAt),
        closesAt: toApiTime(value.closesAt),
        holidays: this.sortedHolidays(),
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: calendar => {
          this.show(calendar);
          this.saving.set(false);
          this.snackBar.open('Business calendar saved.', undefined, { duration: 3000 });
        },
        error: (error: HttpErrorResponse) => {
          this.saving.set(false);
          this.saveError.set(describeSaveError(error, 'Could not save the business calendar.'));
        },
      });
  }

  protected runPreview(): void {
    if (this.previewMinutes.invalid || this.previewing()) {
      return;
    }
    const start = this.previewStart.value ? new Date(this.previewStart.value).toISOString() : null;
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
              ? 'Save the business calendar first.'
              : describeSaveError(error, 'Could not calculate the due date.'),
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
      opensAt: (calendar.opensAt ?? '09:00:00').slice(0, 5),
      closesAt: (calendar.closesAt ?? '17:00:00').slice(0, 5),
    });
    this.workingDays.set(calendar.workingDays);
    this.holidays.set(calendar.holidays);
  }
}
