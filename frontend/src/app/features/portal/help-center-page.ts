import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, debounceTime, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { ARTICLE_TYPES, KnowledgeArticleType, articleTypeLabel } from '../knowledge-base/knowledge-base-models';
import { PortalApi } from './portal-api';
import { HelpCenterArticleSummary, HelpCenterCategory, HelpCenterListParams, NOT_LINKED } from './portal-models';

@Component({
  selector: 'crm-help-center-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DatePipe,
    PageHeader,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './help-center-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './portal-page.scss'],
})
export class HelpCenterPage {
  private readonly api = inject(PortalApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<HelpCenterListParams>();

  protected readonly pageSizeOptions = [10, 20, 50];
  protected readonly types = ARTICLE_TYPES;
  protected readonly typeLabel = articleTypeLabel;
  protected readonly filters = inject(NonNullableFormBuilder).group({
    search: [''],
    type: [null as KnowledgeArticleType | null],
    categoryId: [null as string | null],
  });
  protected readonly categories = signal<HelpCenterCategory[]>([]); // filter hidden when empty / failed; the list still works
  protected readonly items = signal<HelpCenterArticleSummary[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.api
      .categories()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: categories => this.categories.set(categories), error: () => this.categories.set([]) });

    this.requests
      .pipe(
        switchMap(params =>
          this.api.listArticles(params).pipe(
            catchError((error: HttpErrorResponse) => {
              this.error.set(error.status === 404 ? NOT_LINKED : describeHttpError(error, 'Could not load help articles.'));
              this.loading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(result => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.page.set(result.page);
        this.pageSize.set(result.pageSize);
        this.loading.set(false);
      });

    this.filters.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load(1));

    this.load(1);
  }

  protected hasFilters(): boolean {
    const f = this.filters.getRawValue();
    return !!(f.search.trim() || f.type || f.categoryId);
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.load(event.pageIndex + 1);
  }

  protected reload(): void {
    this.load(this.page());
  }

  private load(page: number): void {
    const f = this.filters.getRawValue();
    this.loading.set(true);
    this.error.set(null);
    this.requests.next({ page, pageSize: this.pageSize(), search: f.search, type: f.type, categoryId: f.categoryId });
  }
}
