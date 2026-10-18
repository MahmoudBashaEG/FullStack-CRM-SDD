import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';
import { I18n } from '../i18n/i18n';
import { ACCENT_PRESETS, AccentPreset, BrandingApi, PublicBranding } from './branding-api';

/** PLAT-001 D7: public branding (organization name, accent, logo). The name comes from the ADMIN-001 settings display name (single source). */
@Injectable({ providedIn: 'root' })
export class Branding {
  private readonly api = inject(BrandingApi);
  private readonly i18n = inject(I18n);
  private readonly root = inject(DOCUMENT).documentElement;

  readonly displayName = signal<string | null>(null);
  readonly accent = signal<AccentPreset | null>(null);
  readonly logoVersion = signal<string | null>(null);
  /** Set when the logo image fails to load (404 / broken); the icon is shown instead. */
  readonly logoFailed = signal(false);

  readonly orgName = computed(() => this.displayName() ?? this.i18n.t('app.name'));
  readonly logoUrl = computed(() => {
    const version = this.logoVersion();
    return version && !this.logoFailed() ? this.api.logoUrl(version) : null;
  });

  /** App initializer. Never rejects: the sign-in page must never be blocked by branding. */
  async load(): Promise<void> {
    try {
      this.apply(await firstValueFrom(this.api.get().pipe(timeout(5000))));
    } catch {
      this.apply({ displayName: null, accentColor: null, logoVersion: null });
    }
  }

  /** Called after the settings page changed the name, accent or logo. */
  reload(): Promise<void> {
    return this.load();
  }

  private apply(branding: PublicBranding): void {
    const accent = branding.accentColor && (ACCENT_PRESETS as readonly string[]).includes(branding.accentColor) ? branding.accentColor : null;
    this.displayName.set(branding.displayName?.trim() || null);
    this.accent.set(accent);
    if (this.logoVersion() !== branding.logoVersion) {
      this.logoFailed.set(false);
    }
    this.logoVersion.set(branding.logoVersion);
    this.root.dataset['accent'] = accent ?? 'violet';
  }
}
