import { TestBed } from '@angular/core/testing';
import { MAT_DATE_LOCALE } from '@angular/material/core';
import { fromLocalInput } from '../../features/tasks/task-models';
import { CrmDateAdapter } from './crm-date-adapter';
import { combineLocalDateTime, fromDateOnly, fromTimeOfDay, splitIsoToLocal, toDateOnly, toTimeOfDay } from './date-only';

describe('date-only and local date-time conversion', () => {
  it('keeps picked calendar days and wall-clock times exact in any timezone (no Cairo shift)', () => {
    expect(toDateOnly(new Date(2030, 0, 31))).toBe('2030-01-31');
    expect(toDateOnly(new Date(2030, 11, 31, 23, 59))).toBe('2030-12-31');
    expect(toDateOnly(null)).toBeNull();

    const parsed = fromDateOnly('2030-01-31');
    expect([parsed?.getFullYear(), parsed?.getMonth(), parsed?.getDate()]).toEqual([2030, 0, 31]);
    expect(fromDateOnly('2030-02-31')).toBeNull();
    expect(fromDateOnly('31/01/2030')).toBeNull();
    expect(toDateOnly(fromDateOnly('2030-07-01'))).toBe('2030-07-01');

    // Same instant the old <input type="datetime-local"> path produced for the same wall time.
    const iso = combineLocalDateTime(new Date(2030, 0, 31), new Date(2000, 0, 1, 9, 30));
    expect(iso).toBe(new Date(2030, 0, 31, 9, 30).toISOString());
    expect(iso).toBe(fromLocalInput('2030-01-31T09:30'));
    expect(combineLocalDateTime(new Date(2030, 0, 31), null)).toBeNull();
    const split = splitIsoToLocal(iso);
    expect([split.day?.getDate(), split.time?.getHours(), split.time?.getMinutes()]).toEqual([31, 9, 30]);

    expect(toTimeOfDay(fromTimeOfDay('08:05'))).toBe('08:05');
    expect(fromTimeOfDay('25:00')).toBeNull();

    // C2: typed dates are deterministic in English and Arabic (local midnight, year-month-day only, Arabic-Indic digits allowed).
    TestBed.configureTestingModule({ providers: [CrmDateAdapter, { provide: MAT_DATE_LOCALE, useValue: 'en-US' }] });
    const adapter = TestBed.inject(CrmDateAdapter);
    expect(toDateOnly(adapter.parse('2030-01-31'))).toBe('2030-01-31');
    expect(toDateOnly(adapter.parse('٢٠٣٠-٠١-٣١'))).toBe('2030-01-31');
    expect(toDateOnly(adapter.parse('2030/1/5'))).toBe('2030-01-05');
    expect(adapter.isValid(adapter.parse('2030-02-31')!)).toBe(false);
    expect(adapter.isValid(adapter.parse('05/06/2030')!)).toBe(false);
    expect(adapter.parse('')).toBeNull();
    expect(adapter.format(new Date(2030, 0, 5), 'input')).toBe('2030-01-05');
    adapter.setLocale('ar-EG-u-nu-latn');
    expect(adapter.format(new Date(2030, 0, 5), 'input')).toBe('2030-01-05');
    expect(adapter.format(new Date(2030, 0, 5, 14, 7), 'time24')).toBe('14:07');
    expect(adapter.getFirstDayOfWeek()).toBe(6);
  });
});
