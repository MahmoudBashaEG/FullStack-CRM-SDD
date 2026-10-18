import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';
import { readPreference, writePreference } from './preferences-store';

export type ThemeMode = 'light' | 'dark';

/** PLAT-001 D6/D10: light/dark per browser. `mode` null = follow the OS (prefers-color-scheme) until the user chooses. */
@Injectable({ providedIn: 'root' })
export class Theme {
  private readonly root = inject(DOCUMENT).documentElement;
  readonly mode = signal<ThemeMode | null>(null);
  /** Increments on every change; charts read it to re-read the CSS color variables. */
  readonly version = signal(0);

  /** The effective mode for the toggle icon. */
  readonly effective = computed<ThemeMode>(() => this.mode() ?? (this.prefersDark() ? 'dark' : 'light'));

  init(): void {
    const stored = readPreference('crm.theme');
    this.apply(stored === 'light' || stored === 'dark' ? stored : null, false);
  }

  toggle(): void {
    this.apply(this.effective() === 'dark' ? 'light' : 'dark', true);
  }

  private prefersDark(): boolean {
    return typeof matchMedia === 'function' && matchMedia('(prefers-color-scheme: dark)').matches;
  }

  private apply(mode: ThemeMode | null, persist: boolean): void {
    this.mode.set(mode);
    if (mode) {
      this.root.dataset['theme'] = mode;
    } else {
      delete this.root.dataset['theme'];
    }
    this.version.update((v) => v + 1);
    if (persist) {
      writePreference('crm.theme', mode);
    }
  }
}
