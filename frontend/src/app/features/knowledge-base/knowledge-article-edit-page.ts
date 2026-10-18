import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/describe-http-error';
import { I18n } from '../../core/i18n/i18n';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { describeSaveError } from '../sla/sla-errors';
import { KnowledgeBaseApi } from './knowledge-base-api';
import {
  ARTICLE_TYPES,
  ARTICLE_VISIBILITIES,
  KB_LIMITS,
  KnowledgeArticle,
  KnowledgeCategory,
  articleStatusLabel,
  articleTypeLabel,
} from './knowledge-base-models';
import { MarkdownView } from './markdown-view';

@Component({
  selector: 'crm-knowledge-article-edit-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    TranslatePipe,
    MatCardModule,
    MatIconModule,
    MarkdownView,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTabsModule,
    MatButtonModule,
  ],
  templateUrl: './knowledge-article-edit-page.html',
  styleUrl: './knowledge-article-edit-page.scss',
})
export class KnowledgeArticleEditPage {
  protected readonly i18n = inject(I18n);
  private readonly api = inject(KnowledgeBaseApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);

  protected readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id');
  protected readonly isNew = this.id === null;
  protected readonly types = ARTICLE_TYPES;
  protected readonly visibilities = ARTICLE_VISIBILITIES;
  protected readonly typeLabel = articleTypeLabel;
  protected readonly limits = KB_LIMITS;
  protected readonly article = signal<KnowledgeArticle | null>(null);
  protected readonly categories = signal<KnowledgeCategory[]>([]);
  protected readonly categoriesLoaded = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly readOnly = computed(() => !this.isNew && this.article() !== null && !this.article()!.canEdit);
  protected readonly noCategories = computed(() => this.categoriesLoaded() && this.categories().length === 0);
  protected readonly form = inject(NonNullableFormBuilder).group({
    title: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(KB_LIMITS.title)]],
    summary: ['', Validators.maxLength(KB_LIMITS.summary)],
    type: ['Solution' as KnowledgeArticle['type'], Validators.required],
    categoryId: ['', Validators.required],
    visibility: ['Internal' as KnowledgeArticle['visibility'], Validators.required],
    body: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(KB_LIMITS.body)]],
  });

  constructor() {
    this.api.listCategories(false).subscribe({
      next: categories => {
        this.categories.set(categories);
        this.categoriesLoaded.set(true);
        this.ensureCurrentCategory();
      },
      error: (error: HttpErrorResponse) => this.loadError.set(describeHttpError(error, this.i18n.t('kb.edit.categoriesFailed'), this.i18n.t)),
    });

    if (this.id) {
      this.api.getArticle(this.id).subscribe({
        next: a => this.apply(a),
        error: (error: HttpErrorResponse) =>
          this.loadError.set(error.status === 404 ? this.i18n.t('kb.edit.notAvailable') : describeHttpError(error, this.i18n.t('kb.edit.loadFailed'), this.i18n.t)),
      });
    }
  }

  protected statusLine(): string {
    const a = this.article();
    const t = this.i18n.t;
    return a ? t('kb.edit.statusLine', { status: articleStatusLabel(a.status, t) }) : t('kb.edit.newStatusLine');
  }

  protected save(): void {
    if (this.form.invalid || this.busy() || this.noCategories()) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const body = {
      title: v.title.trim(),
      summary: v.summary.trim() || null,
      body: v.body.trim(),
      type: v.type,
      categoryId: v.categoryId,
      visibility: v.visibility,
    };
    this.busy.set(true);
    this.error.set(null);
    (this.id ? this.api.updateArticle(this.id, body) : this.api.createArticle(body)).subscribe({
      next: a => {
        this.busy.set(false);
        this.snackBar.open(this.i18n.t('kb.edit.saved'), this.i18n.t('quickReplies.ok'), { duration: 4000 });
        void this.router.navigate(['/knowledge-base', a.id]);
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(describeSaveError(error, this.i18n.t('kb.edit.saveFailed'), this.i18n.t));
      },
    });
  }

  private apply(a: KnowledgeArticle): void {
    this.article.set(a);
    this.form.reset({
      title: a.title, summary: a.summary ?? '', type: a.type, categoryId: a.categoryId, visibility: a.visibility, body: a.body,
    });
    this.ensureCurrentCategory();
  }

  // An unchanged inactive category stays selectable (the server accepts it unchanged).
  private ensureCurrentCategory(): void {
    const a = this.article();
    if (a && this.categoriesLoaded() && !this.categories().some(c => c.id === a.categoryId)) {
      this.categories.update(list => [...list, { id: a.categoryId, name: this.i18n.t('kb.categoryInactive', { name: a.categoryName }), isActive: false, updatedAtUtc: a.updatedAtUtc }]);
    }
  }
}
