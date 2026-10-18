import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../api/api-url';

export const ACCENT_PRESETS = ['violet', 'blue', 'azure', 'green', 'orange', 'rose'] as const;
export type AccentPreset = (typeof ACCENT_PRESETS)[number];

/** Matches Crm.Api PublicBrandingResponse (anonymous). logoVersion changes on every logo replacement; null = no logo. */
export interface PublicBranding {
  displayName: string | null;
  accentColor: AccentPreset | null;
  logoVersion: string | null;
}

@Injectable({ providedIn: 'root' })
export class BrandingApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  get(): Observable<PublicBranding> {
    return this.http.get<PublicBranding>(this.apiUrl.versioned('branding'));
  }

  /** The version is the cache-busting key: a new logo always has a new URL. */
  logoUrl(version: string): string {
    return `${this.apiUrl.versioned('branding/logo')}?v=${encodeURIComponent(version)}`;
  }
}
