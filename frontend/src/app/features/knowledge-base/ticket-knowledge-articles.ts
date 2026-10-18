import { Clipboard } from '@angular/cdk/clipboard';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, debounceTime, startWith, switchMap, tap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { copyQuickReply } from '../quick-replies/copy-quick-reply';
import { KnowledgeBaseApi } from './knowledge-base-api';
import {
  KnowledgeArticle,
  KnowledgeArticleSummary,
  TicketKnowledgeLink,
  articleStatusLabel,
  articleStatusTone,
  articleTypeLabel,
} from './knowledge-base-models';
import { MarkdownView } from './markdown-view';

/** KB-001 K4 card for the authorized Ticket Details page. Search/read/copy published articles and link/unlink them. Copy only — nothing is sent. */
@Component({
  selector: 'crm-ticket-knowledge-articles',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe, LocalizedDatePipe, ErrorState, StatusBadge, MarkdownView, MatButtonModule, MatFormFieldModule, MatInputModule, MatIconModule],
  templateUrl: './ticket-knowledge-articles.html',
  styleUrl: '../../shared/ui/productivity-page.scss',
})
export class TicketKnowledgeArticles implements OnInit {
  protected readonly i18n = inject(I18n);
  private readonly api = inject(KnowledgeBaseApi);
  private readonly clipboard = inject(Clipboard);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly refreshSearch = new Subject<void>();

  readonly ticketId = input.required<string>();
  /** The caller lost ticket access (the link list returned 404): the parent shows its "no longer available" state (as TicketDiscussion). */
  readonly accessLost = output<void>();

  protected readonly typeLabel = articleTypeLabel;
  protected readonly statusLabel = articleStatusLabel;
  protected readonly statusTone = articleStatusTone;
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly links = signal<TicketKnowledgeLink[]>([]);
  protected readonly results = signal<KnowledgeArticleSummary[]>([]);
  protected readonly selected = signal<KnowledgeArticle | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly rowError = signal<{ id: string; message: string } | null>(null);
  protected readonly busy = signal(false);

  constructor() {
    this.search.valueChanges
      .pipe(startWith(''), debounceTime(300), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.refreshSearch.next());

    this.refreshSearch
      .pipe(
        tap(() => this.error.set(null)),
        switchMap(() =>
          this.api
            .listArticles({ page: 1, pageSize: 5, search: this.search.value, type: null, categoryId: null, status: 'Published', mine: false })
            .pipe(
              catchError((error: HttpErrorResponse) => {
                this.error.set(describeHttpError(error, this.i18n.t('kb.ticket.searchFailed'), this.i18n.t));
                return EMPTY;
              }),
            ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(result => this.results.set(result.items));

  }

  ngOnInit(): void {
    this.reloadLinks();
  }

  protected isLinked(articleId: string): boolean {
    return this.links().some(l => l.articleId === articleId);
  }

  /** Only a 404 from the link list means the ticket itself is gone for this caller. */
  protected reloadLinks(): void {
    this.loadError.set(null);
    this.api.listTicketLinks(this.ticketId()).subscribe({
      next: links => this.links.set(links),
      error: (error: HttpErrorResponse) => {
        if (error.status === 404) {
          this.accessLost.emit();
          return;
        }
        this.loadError.set(describeHttpError(error, this.i18n.t('kb.ticket.linksFailed'), this.i18n.t));
      },
    });
  }

  protected read(article: KnowledgeArticleSummary): void {
    if (this.selected()?.id === article.id) {
      this.selected.set(null);
      return;
    }
    this.rowError.set(null);
    this.api.getArticle(article.id).subscribe({
      next: a => this.selected.set(a),
      error: (error: HttpErrorResponse) => {
        this.selected.set(null);
        this.rowError.set({
          id: article.id,
          message: error.status === 404 ? this.i18n.t('kb.ticket.unavailable') : describeHttpError(error, this.i18n.t('kb.ticket.loadFailed'), this.i18n.t),
        });
        if (error.status === 404) {
          this.refreshSearch.next();
        }
      },
    });
  }

  protected copy(article: KnowledgeArticle): void {
    copyQuickReply(this.clipboard, this.snackBar, article.body, this.i18n.t);
  }

  protected link(article: KnowledgeArticleSummary): void {
    this.busy.set(true);
    this.error.set(null);
    this.api.linkArticle(this.ticketId(), article.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.snackBar.open(this.i18n.t('kb.ticket.linkedToast'), this.i18n.t('quickReplies.ok'), { duration: 4000 });
        this.reloadLinks();
      },
      error: (error: HttpErrorResponse) => this.onChangeFailed(error, this.i18n.t('kb.ticket.linkFailed'), true),
    });
  }

  protected unlink(link: TicketKnowledgeLink): void {
    this.busy.set(true);
    this.error.set(null);
    this.api.unlinkArticle(this.ticketId(), link.articleId).subscribe({
      next: () => {
        this.busy.set(false);
        this.snackBar.open(this.i18n.t('kb.ticket.unlinkedToast'), this.i18n.t('quickReplies.ok'), { duration: 4000 });
        this.reloadLinks();
      },
      error: (error: HttpErrorResponse) => this.onChangeFailed(error, this.i18n.t('kb.ticket.unlinkFailed'), false),
    });
  }

  // 404 on link/unlink is ambiguous (article gone, already unlinked, or ticket gone): never emit accessLost here.
  // The link-list reload decides: it emits accessLost only when the ticket itself returns 404.
  private onChangeFailed(error: HttpErrorResponse, fallback: string, rerunSearch: boolean): void {
    this.busy.set(false);
    this.error.set(describeHttpError(error, fallback, this.i18n.t));
    this.reloadLinks();
    if (rerunSearch) {
      this.refreshSearch.next();
    }
  }
}
