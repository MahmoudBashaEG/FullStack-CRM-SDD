import { Clipboard } from '@angular/cdk/clipboard';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { AuthService } from '../../core/auth/auth-service';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { copyQuickReply } from '../quick-replies/copy-quick-reply';
import { KnowledgeBaseApi } from './knowledge-base-api';
import {
  KB_COPY_HINT,
  KB_LIMITS,
  KnowledgeArticle,
  PUBLIC_VISIBILITY_HINT,
  articleStatusLabel,
  articleStatusTone,
  articleTypeLabel,
} from './knowledge-base-models';
import { MarkdownView } from './markdown-view';

const NOT_AVAILABLE = 'This article is not available to you. It may be unpublished or archived.';

@Component({
  selector: 'crm-knowledge-article-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DatePipe,
    PageHeader,
    StatusBadge,
    MarkdownView,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
  ],
  templateUrl: './knowledge-article-page.html',
  styleUrl: './knowledge-article-page.scss',
})
export class KnowledgeArticlePage {
  private readonly api = inject(KnowledgeBaseApi);
  private readonly auth = inject(AuthService);
  private readonly clipboard = inject(Clipboard);
  private readonly snackBar = inject(MatSnackBar);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';

  protected readonly copyHint = KB_COPY_HINT;
  protected readonly publicHint = PUBLIC_VISIBILITY_HINT;
  protected readonly typeLabel = articleTypeLabel;
  protected readonly statusLabel = articleStatusLabel;
  protected readonly statusTone = articleStatusTone;
  protected readonly isSuperAdmin = computed(() => this.auth.role() === 'SuperAdmin');
  protected readonly article = signal<KnowledgeArticle | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly reason = new FormControl('', { nonNullable: true, validators: [Validators.maxLength(KB_LIMITS.returnReason)] });
  protected readonly reasonMax = KB_LIMITS.returnReason;

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loadError.set(null);
    this.api.getArticle(this.id).subscribe({
      next: a => this.article.set(a),
      error: (error: HttpErrorResponse) =>
        this.loadError.set(error.status === 404 ? NOT_AVAILABLE : describeHttpError(error, 'Could not load the article.')),
    });
  }

  protected copy(a: KnowledgeArticle): void {
    copyQuickReply(this.clipboard, this.snackBar, a.body);
  }

  protected submit(): void {
    this.run(this.api.submit(this.id), 'Submitted for publication.');
  }

  protected returnToDraft(): void {
    if (this.reason.invalid) {
      return;
    }
    this.run(this.api.returnToDraft(this.id, this.reason.value.trim() || null), 'Returned to Draft.', () => this.reason.reset(''));
  }

  protected publish(): void {
    this.run(this.api.publish(this.id), 'Published.');
  }

  protected archive(): void {
    this.run(this.api.archive(this.id), 'Archived.');
  }

  protected restore(): void {
    this.run(this.api.restore(this.id), 'Restored to Draft.');
  }

  private run(request: Observable<KnowledgeArticle>, message: string, done?: () => void): void {
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: a => {
        this.busy.set(false);
        this.article.set(a);
        done?.();
        this.snackBar.open(message, 'OK', { duration: 4000 });
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        if (error.status === 404) {
          this.article.set(null);
          this.loadError.set(NOT_AVAILABLE);
          return;
        }
        this.error.set(describeHttpError(error, 'Could not change the article.'));
        if (error.status === 409) {
          this.load(); // show the current state
        }
      },
    });
  }
}
