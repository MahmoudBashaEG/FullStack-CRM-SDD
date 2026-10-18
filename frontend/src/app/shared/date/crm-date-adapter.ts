import { Injectable } from '@angular/core';
import { DateAdapter, MAT_DATE_FORMATS, MatDateFormats, NativeDateAdapter } from '@angular/material/core';

const ARABIC_INDIC_DIGITS = /[٠-٩۰-۹]/g;
const TYPED_DATE = /^(\d{4})[-/.](\d{1,2})[-/.](\d{1,2})$/;

const pad = (n: number) => String(n).padStart(2, '0');

/**
 * PLAT-001 C2. NativeDateAdapter.parse() delegates to Date.parse, which is engine-defined and treats 'yyyy-MM-dd' as UTC
 * midnight (a different calendar day west of UTC). This adapter makes typing deterministic in English and Arabic:
 *   - the one accepted typed format is year-month-day (yyyy-mm-dd; '/' or '.' also work), Latin or Arabic-Indic digits;
 *   - the result is always a LOCAL-midnight Date, so toDateOnly() yields exactly the typed day in any timezone;
 *   - day-first / month-first strings (05/06/2026) are rejected, never guessed;
 *   - the input shows the same yyyy-mm-dd text it accepts, and times use a 24-hour HH:mm clock.
 */
@Injectable()
export class CrmDateAdapter extends NativeDateAdapter {
  override parse(value: unknown): Date | null {
    if (typeof value === 'number') {
      return new Date(value);
    }
    if (typeof value !== 'string') {
      return value ? this.invalid() : null;
    }
    const text = value.trim().replace(ARABIC_INDIC_DIGITS, (digit) => String(digit.charCodeAt(0) & 0xf));
    if (text === '') {
      return null;
    }
    const match = TYPED_DATE.exec(text);
    if (!match) {
      return this.invalid();
    }
    const [year, month, day] = [Number(match[1]), Number(match[2]), Number(match[3])];
    const date = new Date(year, month - 1, day);
    return date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day ? date : this.invalid();
  }

  override format(date: Date, displayFormat: unknown): string {
    if (displayFormat === 'input') {
      return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
    }
    if (displayFormat === 'time24') {
      return `${pad(date.getHours())}:${pad(date.getMinutes())}`;
    }
    return super.format(date, displayFormat as object);
  }

  override getFirstDayOfWeek(): number {
    return this.locale?.toString().startsWith('ar') ? 6 : 0; // Egypt's week starts on Saturday
  }
}

export const CRM_DATE_FORMATS: MatDateFormats = {
  parse: { dateInput: 'input', timeInput: 'time24' },
  display: {
    dateInput: 'input',
    monthLabel: { month: 'short' },
    monthYearLabel: { year: 'numeric', month: 'short' },
    dateA11yLabel: { year: 'numeric', month: 'long', day: 'numeric' },
    monthYearA11yLabel: { year: 'numeric', month: 'long' },
    timeInput: 'time24',
    timeOptionLabel: 'time24',
  },
};

export const provideCrmDateAdapter = () => [
  { provide: DateAdapter, useClass: CrmDateAdapter },
  { provide: MAT_DATE_FORMATS, useValue: CRM_DATE_FORMATS },
];
