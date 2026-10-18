import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { Branding } from '../../branding/branding';
import { I18n } from '../../i18n/i18n';
import { TopbarControls } from '../topbar-controls';

/**
 * Branded layout of /sign-in and /sign-up: organization logo + name, language and theme switchers, and the projected form card.
 * Branding is public (anonymous) data, loaded before the first render.
 */
@Component({
  selector: 'crm-auth-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, TopbarControls],
  template: `
    <div class="auth">
      <aside class="auth__brand">
        @if (branding.logoUrl(); as logo) {
          <img class="auth__logo" [src]="logo" alt="" (error)="branding.logoFailed.set(true)" />
        } @else {
          <span class="auth__logo-icon" aria-hidden="true"><mat-icon>support_agent</mat-icon></span>
        }
        <p class="auth__org">{{ branding.orgName() }}</p>
        <p class="auth__tagline">{{ i18n.t('app.tagline') }}</p>
      </aside>
      <main class="auth__panel">
        <div class="auth__controls"><crm-topbar-controls /></div>
        <div class="auth__card"><ng-content /></div>
      </main>
    </div>
  `,
  styles: `
    :host {
      display: block;
      min-block-size: 100dvh;
    }
    .auth {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      min-block-size: 100dvh;
    }
    .auth__brand {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: var(--crm-space-2);
      padding: var(--crm-space-6) var(--crm-space-4);
      background: linear-gradient(160deg, var(--mat-sys-primary), var(--mat-sys-tertiary));
      color: var(--mat-sys-on-primary);
      text-align: center;
    }
    .auth__logo,
    .auth__logo-icon {
      inline-size: 72px;
      block-size: 72px;
      border-radius: var(--crm-radius-lg);
      background: var(--mat-sys-surface);
      color: var(--mat-sys-primary);
      object-fit: contain;
      display: grid;
      place-items: center;
    }
    .auth__logo-icon mat-icon {
      font-size: 40px;
      inline-size: 40px;
      block-size: 40px;
    }
    .auth__org {
      margin: var(--crm-space-2) 0 0;
      font: var(--mat-sys-headline-small);
      overflow-wrap: anywhere;
    }
    .auth__tagline {
      margin: 0;
      font: var(--mat-sys-body-large);
      opacity: 0.9;
    }
    .auth__panel {
      position: relative;
      display: flex;
      flex-direction: column;
      justify-content: center;
      padding: var(--crm-space-4);
    }
    .auth__controls {
      position: absolute;
      inset-block-start: var(--crm-space-2);
      inset-inline-end: var(--crm-space-2);
    }
    .auth__card {
      inline-size: 100%;
      max-inline-size: 440px;
      margin-inline: auto;
      padding-block-start: var(--crm-space-8);
    }
    @media (min-width: 900px) {
      .auth {
        grid-template-columns: minmax(320px, 5fr) minmax(0, 7fr);
      }
      .auth__brand {
        padding: var(--crm-space-8);
      }
    }
  `,
})
export class AuthLayout {
  protected readonly branding = inject(Branding);
  protected readonly i18n = inject(I18n);
}
