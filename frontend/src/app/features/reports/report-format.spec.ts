import { convertToParamMap } from '@angular/router';
import { barPercent, formatAverage, formatBucketLabel, formatPercent, formatUtcDay, parsePeriodParams, toPeriodQueryParams } from './report-format';

describe('report formatting and shared period', () => {
  it('prints N/A instead of 0 % for undefined rates, scales bars, prints UTC days and round-trips the shared period query', () => {
    expect(formatPercent(null)).toBe('N/A');
    expect(formatPercent(75)).toBe('75.0%');
    expect(formatAverage(null)).toBe('N/A');
    expect(formatAverage(4.333)).toBe('4.33 / 5');
    expect(barPercent(0, 0)).toBe(0);
    expect(barPercent(5, 20)).toBe(25);

    // Date-only API values are UTC calendar days: never shifted by the browser timezone (e.g. Africa/Cairo is UTC+2/+3).
    expect(formatUtcDay('2030-01-01')).toBe('Jan 1, 2030');
    expect(formatUtcDay('2030-07-31')).toBe('Jul 31, 2030');
    expect(formatBucketLabel('2030-01-01', '2030-01-01')).toBe('Jan 1');
    expect(formatBucketLabel('2030-12-30', '2031-01-05')).toBe('Dec 30 – Jan 5');

    expect(parsePeriodParams(convertToParamMap({}))).toEqual({ preset: 'Last30Days', from: null, to: null });
    expect(parsePeriodParams(convertToParamMap({ preset: 'Last7Days', from: '2030-01-01' }))).toEqual({ preset: 'Last7Days', from: null, to: null });
    expect(parsePeriodParams(convertToParamMap({ preset: 'Custom', from: '2030-01-01', to: '2030-01-31' })))
      .toEqual({ preset: 'Custom', from: '2030-01-01', to: '2030-01-31' });
    expect(parsePeriodParams(convertToParamMap({ preset: 'Custom', from: '2030-01-01' }))).toEqual({ preset: 'Last30Days', from: null, to: null });
    expect(parsePeriodParams(convertToParamMap({ preset: 'Yearly' }))).toEqual({ preset: 'Last30Days', from: null, to: null });
    expect(toPeriodQueryParams({ preset: 'Last90Days', from: '2030-01-01', to: '2030-01-02' })).toEqual({ preset: 'Last90Days', from: null, to: null });
  });
});
