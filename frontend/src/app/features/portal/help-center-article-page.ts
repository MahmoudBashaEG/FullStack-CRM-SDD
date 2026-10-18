import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EMPTY, catchError, map, switchMap, tap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { MarkdownView } from '../knowledge-base/markdown-view';
import { PortalApi } from './portal-api';
import { HelpCenterArticle } from './portal-models';

@Component({
  selector: 'crm-help-center-article-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, LocalizedDatePipe, TranslatePipe, ErrorState, MarkdownView, MatCardModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  templateUrl: './help-center-article-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './portal-page.scss'],
})
export class HelpCenterArticlePage {
  private readonly api = inject(PortalApi);
  private readonly i18n = inject(I18n);

  protected readonly article = signal<HelpCenterArticle | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);

  constructor() {
    // paramMap + switchMap: a changed :id reloads, clears the previous article first and cancels the old request.
    inject(ActivatedRoute).paramMap
      .pipe(
        map(params => params.get('id') ?? ''),
        tap(() => {
          this.article.set(null);
          this.loadError.set(null);
          this.loading.set(true);
        }),
        switchMap(id =>
          this.api.getArticle(id).pipe(
            catchError((error: HttpErrorResponse) => {
              const detail = (error.error as { detail?: string } | null)?.detail;
              this.loadError.set(
                error.status === 404
                  ? (detail === 'Customer profile not found.' ? this.i18n.t('portal.notLinked') : this.i18n.t('portal.helpCenter.unavailable'))
                  : describeHttpError(error, this.i18n.t('portal.helpCenter.articleLoadError'), this.i18n.t),
              );
              this.loading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe(article => {
        this.article.set(article);
        this.loading.set(false);
      });
  }
}
