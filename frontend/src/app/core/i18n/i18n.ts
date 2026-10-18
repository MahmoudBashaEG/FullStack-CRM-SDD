import { Directionality } from '@angular/cdk/bidi';
import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';
import { DateAdapter } from '@angular/material/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { readPreference, writePreference } from '../preferences/preferences-store';
import { EN } from './en';
import { Translate, lookup } from './translate-fn';

export type Language = 'en' | 'ar';

/** PLAT-001 D3/D4: signal-based language switch with no reload. English is bundled; Arabic is a lazy chunk loaded before first render. */
@Injectable({ providedIn: 'root' })
export class I18n {
  private readonly dir = inject(Directionality);
  private readonly dateAdapter = inject<DateAdapter<unknown>>(DateAdapter);
  private readonly document = inject(DOCUMENT);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dictionary = signal<object>(EN);

  readonly language = signal<Language>('en');
  readonly direction = computed<'rtl' | 'ltr'>(() => (this.language() === 'ar' ? 'rtl' : 'ltr'));
  /** Intl locale: Arabic month and day names with Latin digits (D4), so IDs, counts and typed dates read the same in both languages. */
  readonly locale = computed(() => (this.language() === 'ar' ? 'ar-EG-u-nu-latn' : 'en-US'));

  /** App initializer: the stored language (default English, never inferred from location). Awaited before the first render. */
  init(): Promise<void> {
    return this.use(readPreference('crm.lang') === 'ar' ? 'ar' : 'en', false);
  }

  async use(language: Language, persist = true): Promise<void> {
    let dictionary: object = EN;
    if (language === 'ar') {
      try {
        dictionary = (await import('./ar')).AR;
      } catch {
        // Offline / chunk missing: keep English; the stored preference is left as-is so a reload retries.
        this.snackBar.open(lookup(EN, 'errors.languageLoad') ?? '', undefined, { duration: 6000 });
        return;
      }
    }

    this.dictionary.set(dictionary);
    this.language.set(language);
    const dir = language === 'ar' ? 'rtl' : 'ltr';
    const root = this.document.documentElement;
    root.lang = language;
    root.dir = dir;
    this.dir.valueSignal.set(dir); // root CDK Directionality: overlays (dialogs, menus, datepicker, snackbars) follow
    this.dir.change.emit(dir);
    this.dateAdapter.setLocale(this.locale());
    if (persist) {
      writePreference('crm.lang', language);
    }
  }

  /** Missing key ⇒ English, then the key itself (visible in review). Reads the dictionary signal, so templates re-render on switch. */
  readonly t: Translate = (key, params) => lookup(this.dictionary(), key, params) ?? lookup(EN, key, params) ?? key;
}
