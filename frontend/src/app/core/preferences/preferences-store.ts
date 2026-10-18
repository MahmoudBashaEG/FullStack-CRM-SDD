/** PLAT-001 D6: per-browser preferences. Never secrets; the access token is never stored. Private mode / blocked storage ⇒ defaults. */
export type PreferenceKey = 'crm.lang' | 'crm.theme' | 'crm.navCollapsed';

export function readPreference(key: PreferenceKey): string | null {
  try {
    return globalThis.localStorage?.getItem(key) ?? null;
  } catch {
    return null;
  }
}

export function writePreference(key: PreferenceKey, value: string | null): void {
  try {
    if (value === null) {
      globalThis.localStorage?.removeItem(key);
    } else {
      globalThis.localStorage?.setItem(key, value);
    }
  } catch {
    /* storage unavailable: the preference lasts for this page only */
  }
}
