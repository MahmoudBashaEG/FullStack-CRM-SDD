import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EMPTY, catchError, map, switchMap, tap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { articleTypeLabel } from '../knowledge-base/knowledge-base-models';
import { MarkdownView } from '../knowledge-base/markdown-view';
import { PortalApi } from './portal-api';
import { HelpCenterArticle, NOT_LINKED } from './portal-models';

@Component({
  selector: 'crm-help-center-article-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, MarkdownView, MatCardModule, MatButtonModule, MatProgressBarModule],
  templateUrl: './help-center-article-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './portal-page.scss'],
})
export class HelpCenterArticlePage {
  private readonly api = inject(PortalApi);

  protected readonly typeLabel = articleTypeLabel;
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
                  ? (detail === 'Customer profile not found.' ? NOT_LINKED : 'This article is not available.')
                  : describeHttpError(error, 'Could not load the article.'),
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
