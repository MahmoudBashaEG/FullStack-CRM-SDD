import { DOCUMENT } from '@angular/common';
import { Injectable, effect, inject, signal } from '@angular/core';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { Branding } from '../branding/branding';
import { I18n } from '../i18n/i18n';

/** Route `title` is a translation key (e.g. 'titles.tickets'); the document title is "<title> | <organization>" and follows language changes. */
@Injectable({ providedIn: 'root' })
export class CrmTitleStrategy extends TitleStrategy {
  private readonly document = inject(DOCUMENT);
  private readonly i18n = inject(I18n);
  private readonly branding = inject(Branding);
  private readonly key = signal<string | undefined>(undefined);

  constructor() {
    super();
    effect(() => {
      const key = this.key();
      this.document.title = key ? `${this.i18n.t(key)} | ${this.branding.orgName()}` : this.branding.orgName();
    });
  }

  override updateTitle(snapshot: RouterStateSnapshot): void {
    this.key.set(this.buildTitle(snapshot));
  }
}
