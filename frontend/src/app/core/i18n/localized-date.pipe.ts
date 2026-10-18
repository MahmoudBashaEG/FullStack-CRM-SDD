import { Pipe, PipeTransform, inject } from '@angular/core';
import { I18n } from './i18n';

export type CrmDateFormat = 'short' | 'medium' | 'long' | 'full' | 'date' | 'time';

const OPTIONS: Record<CrmDateFormat, Intl.DateTimeFormatOptions> = {
  short: { dateStyle: 'short', timeStyle: 'short' },
  medium: { dateStyle: 'medium', timeStyle: 'short' },
  long: { dateStyle: 'long', timeStyle: 'short' },
  full: { dateStyle: 'full', timeStyle: 'short' },
  date: { dateStyle: 'medium' },
  time: { timeStyle: 'short' },
};

const cache = new Map<string, Intl.DateTimeFormat>();

export function formatInstant(value: string | Date | null | undefined, locale: string, format: CrmDateFormat = 'medium', timeZone?: 'UTC'): string {
  if (value === null || value === undefined || value === '') {
    return '';
  }
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '';
  }
  const key = `${locale}|${format}|${timeZone ?? ''}`;
  let formatter = cache.get(key);
  if (!formatter) {
    formatter = new Intl.DateTimeFormat(locale, { ...OPTIONS[format], ...(timeZone ? { timeZone } : {}) });
    cache.set(key, formatter);
  }
  return formatter.format(date);
}

/** Locale-aware replacement for `| date` on server TIMESTAMPS (instants). Never use it for date-only yyyy-MM-dd values (use formatUtcDay). */
@Pipe({ name: 'crmDate', pure: false })
export class LocalizedDatePipe implements PipeTransform {
  private readonly i18n = inject(I18n);

  transform(value: string | Date | null | undefined, format: CrmDateFormat = 'medium', timeZone?: 'UTC'): string {
    return formatInstant(value, this.i18n.locale(), format, timeZone);
  }
}
