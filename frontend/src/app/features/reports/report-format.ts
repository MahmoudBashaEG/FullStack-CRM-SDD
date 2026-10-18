import { ParamMap } from '@angular/router';
import { DEFAULT_PRESET, REPORT_PRESETS, ReportPeriodParams, ReportPreset } from './reports-models';

const DATE = /^\d{4}-\d{2}-\d{2}$/;

const UTC_DAY = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', year: 'numeric', timeZone: 'UTC' });
const UTC_DAY_SHORT = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', timeZone: 'UTC' });

/** null ⇒ 'N/A' (never 0 %). Server values already carry 1 decimal. */
export function formatPercent(value: number | null): string {
  return value === null ? 'N/A' : `${value.toFixed(1)}%`;
}

export function formatAverage(value: number | null): string {
  return value === null ? 'N/A' : `${value.toFixed(2)} / 5`;
}

/** Bar width 0–100 relative to the largest value; 0 when everything is 0. */
export function barPercent(value: number, max: number): number {
  return max <= 0 ? 0 : Math.round((100 * value) / max);
}

/**
 * A date-only API value ("2030-01-31", a UTC calendar day) as "Jan 31, 2030".
 * Angular's DatePipe parses date-only strings as LOCAL midnight, so with timezone 'UTC' a browser east of UTC (e.g. Africa/Cairo)
 * would show the previous day. Here the day is anchored at UTC midnight and formatted in UTC: no browser timezone is involved.
 */
export function formatUtcDay(day: string): string {
  return UTC_DAY.format(new Date(`${day}T00:00:00Z`));
}

/** Trend bucket label: "Jan 4" for a daily bucket, "Jan 4 – Jan 10" for a longer one. Same UTC-day rule as formatUtcDay. */
export function formatBucketLabel(start: string, end: string): string {
  const from = UTC_DAY_SHORT.format(new Date(`${start}T00:00:00Z`));
  return start === end ? from : `${from} – ${UTC_DAY_SHORT.format(new Date(`${end}T00:00:00Z`))}`;
}

/** URL ⇒ period. Unknown preset or a Custom without two well-formed dates ⇒ the default (Last 30 days). */
export function parsePeriodParams(params: ParamMap): ReportPeriodParams {
  const preset = params.get('preset');
  if (!preset || !(REPORT_PRESETS as readonly string[]).includes(preset)) {
    return { preset: DEFAULT_PRESET, from: null, to: null };
  }
  if (preset !== 'Custom') {
    return { preset: preset as ReportPreset, from: null, to: null };
  }
  const from = params.get('from');
  const to = params.get('to');
  return from && to && DATE.test(from) && DATE.test(to)
    ? { preset: 'Custom', from, to }
    : { preset: DEFAULT_PRESET, from: null, to: null };
}

/** Period ⇒ URL query params (null removes from/to for presets). */
export function toPeriodQueryParams(p: ReportPeriodParams): Record<string, string | null> {
  return p.preset === 'Custom' ? { preset: p.preset, from: p.from, to: p.to } : { preset: p.preset, from: null, to: null };
}
