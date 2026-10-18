const DATE_ONLY = /^(\d{4})-(\d{2})-(\d{2})$/;
const TIME_OF_DAY = /^(\d{1,2}):(\d{2})$/;
const pad = (n: number) => String(n).padStart(2, '0');

/** Calendar day the user picked (local-midnight Date from the date adapter) ⇒ 'yyyy-MM-dd'. Never via toISOString (Cairo would shift the day). */
export function toDateOnly(date: Date | null | undefined): string | null {
  return date && !Number.isNaN(date.getTime()) ? `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` : null;
}

/** 'yyyy-MM-dd' ⇒ local-midnight Date for the picker; anything else (including 2030-02-31) ⇒ null. */
export function fromDateOnly(value: string | null | undefined): Date | null {
  const match = value ? DATE_ONLY.exec(value) : null;
  if (!match) {
    return null;
  }
  const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
  return date.getMonth() === Number(match[2]) - 1 && date.getDate() === Number(match[3]) ? date : null;
}

/** Picked day + picked time (local wall clock) ⇒ ISO UTC. Same semantics as the former task-models fromLocalInput. Either missing ⇒ null. */
export function combineLocalDateTime(day: Date | null | undefined, time: Date | null | undefined): string | null {
  if (!day || !time || Number.isNaN(day.getTime()) || Number.isNaN(time.getTime())) {
    return null;
  }
  return new Date(day.getFullYear(), day.getMonth(), day.getDate(), time.getHours(), time.getMinutes()).toISOString();
}

/** ISO UTC ⇒ { day, time } local Dates for the pickers (inverse of combineLocalDateTime); null ⇒ nulls. */
export function splitIsoToLocal(iso: string | null | undefined): { day: Date | null; time: Date | null } {
  if (!iso) {
    return { day: null, time: null };
  }
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return { day: null, time: null };
  }
  return { day: new Date(date.getFullYear(), date.getMonth(), date.getDate()), time: date };
}

/** Picker Date ⇒ 'HH:mm' wall-clock text (SLA calendar opens/closes). */
export function toTimeOfDay(time: Date | null | undefined): string | null {
  return time && !Number.isNaN(time.getTime()) ? `${pad(time.getHours())}:${pad(time.getMinutes())}` : null;
}

/** 'HH:mm' ⇒ picker Date on an arbitrary day; invalid (e.g. 25:00) ⇒ null. */
export function fromTimeOfDay(value: string | null | undefined): Date | null {
  const match = value ? TIME_OF_DAY.exec(value) : null;
  if (!match) {
    return null;
  }
  const hours = Number(match[1]);
  const minutes = Number(match[2]);
  return hours <= 23 && minutes <= 59 ? new Date(2000, 0, 1, hours, minutes) : null;
}
