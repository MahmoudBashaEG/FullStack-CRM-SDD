import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { I18n, Language } from '../i18n/i18n';
import { TranslatePipe } from '../i18n/translate.pipe';
import { Theme } from '../preferences/theme';

/** Language and light/dark switchers, shared by the shell top bar and the auth layout. */
@Component({
  selector: 'crm-topbar-controls',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatMenuModule, TranslatePipe],
  template: `
    <button mat-icon-button type="button" [matMenuTriggerFor]="languageMenu" [attr.aria-label]="'shell.language' | t">
      <mat-icon>translate</mat-icon>
    </button>
    <mat-menu #languageMenu="matMenu">
      <button mat-menu-item type="button" lang="en" dir="ltr" [disabled]="i18n.language() === 'en'" (click)="choose('en')">English</button>
      <button mat-menu-item type="button" lang="ar" dir="rtl" [disabled]="i18n.language() === 'ar'" (click)="choose('ar')">العربية</button>
    </mat-menu>
    <button
      mat-icon-button
      type="button"
      [attr.aria-label]="(theme.effective() === 'dark' ? 'shell.themeToLight' : 'shell.themeToDark') | t"
      [attr.aria-pressed]="theme.effective() === 'dark'"
      (click)="theme.toggle()">
      <mat-icon>{{ theme.effective() === 'dark' ? 'light_mode' : 'dark_mode' }}</mat-icon>
    </button>
  `,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
    }
  `,
})
export class TopbarControls {
  protected readonly i18n = inject(I18n);
  protected readonly theme = inject(Theme);

  protected choose(language: Language): void {
    void this.i18n.use(language);
  }
}
