import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, debounceTime, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { AuthService } from '../../core/auth/auth-service';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { KnowledgeBaseApi } from './knowledge-base-api';
import {
  ARTICLE_TYPES,
  KnowledgeArticleListParams,
  KnowledgeArticleStatus,
  KnowledgeArticleSummary,
  KnowledgeArticleType,
  KnowledgeCategory,
  articleStatusLabel,
  articleStatusTone,
  articleTypeLabel,
} from './knowledge-base-models';

type ArticleView = 'published' | 'review' | 'all' | 'archived' | 'mine';

const VIEW_STATUS: Record<ArticleView, KnowledgeArticleStatus | null> = {
  published: 'Published', review: 'ReadyForPublication', all: null, archived: 'Archived', mine: null,
};

@Component({
  selector: 'crm-knowledge-base-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DatePipe,
    PageHeader,
    StatusBadge,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './knowledge-base-page.html',
  styleUrl: './knowledge-base-page.scss',
})
export class KnowledgeBasePage {
  private readonly api = inject(KnowledgeBaseApi);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<KnowledgeArticleListParams>();

  protected readonly isSuperAdmin = computed(() => this.auth.role() === 'SuperAdmin');
  protected readonly types = ARTICLE_TYPES;
  protected readonly typeLabel = articleTypeLabel;
  protected readonly statusLabel = articleStatusLabel;
  protected readonly statusTone = articleStatusTone;
  protected readonly pageSizeOptions = [10, 20, 50];
  protected readonly views: readonly { value: ArticleView; label: string }[] = this.isSuperAdmin()
    ? [
        { value: 'published', label: 'Published' },
        { value: 'review', label: 'Ready for Publication' },
        { value: 'all', label: 'All statuses' },
        { value: 'archived', label: 'Archived' },
        { value: 'mine', label: 'My articles' },
      ]
    : [
        { value: 'published', label: 'Published' },
        { value: 'mine', label: 'My articles' },
      ];
  protected readonly filters = inject(NonNullableFormBuilder).group({
    search: [''],
    type: [null as KnowledgeArticleType | null],
    categoryId: [null as string | null],
    view: [this.initialView()],
  });
  protected readonly categories = signal<KnowledgeCategory[]>([]);
  protected readonly categoriesLoaded = signal(false);
  protected readonly items = signal<KnowledgeArticleSummary[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.requests
      .pipe(
        switchMap(params =>
          this.api.listArticles(params).pipe(
            catchError((error: HttpErrorResponse) => {
              this.error.set(describeHttpError(error, 'Could not load articles.'));
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

    this.api.listCategories(this.isSuperAdmin()).subscribe({
      next: categories => {
        this.categories.set(categories);
        this.categoriesLoaded.set(true);
      },
      error: () => this.categoriesLoaded.set(false),
    });

    this.filters.valueChanges.pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load(1));
    this.load(1);
  }

  protected noCategories(): boolean {
    return this.categoriesLoaded() && this.categories().length === 0;
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.load(event.pageIndex + 1);
  }

  protected reload(): void {
    this.load(this.page());
  }

  // Optional ?view= deep link, read once. The server re-applies visibility, so a crafted value never widens access.
  private initialView(): ArticleView {
    const requested = inject(ActivatedRoute).snapshot.queryParamMap.get('view') as ArticleView | null;
    return requested && this.views.some(v => v.value === requested) ? requested : 'published';
  }

  private load(page: number): void {
    const f = this.filters.getRawValue();
    this.loading.set(true);
    this.error.set(null);
    this.requests.next({
      page,
      pageSize: this.pageSize(),
      search: f.search,
      type: f.type,
      categoryId: f.categoryId,
      status: VIEW_STATUS[f.view],
      mine: f.view === 'mine',
    });
  }
}
