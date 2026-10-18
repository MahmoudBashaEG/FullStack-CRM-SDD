import { Pipe, PipeTransform, inject } from '@angular/core';
import { I18n } from './i18n';
import { TranslateParams } from './translate-fn';

/** `{{ 'tickets.title' | t }}`. Impure on purpose: the result depends on the language signal (a small object walk). */
@Pipe({ name: 't', pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly i18n = inject(I18n);

  transform(key: string, params?: TranslateParams): string {
    return this.i18n.t(key, params);
  }
}
