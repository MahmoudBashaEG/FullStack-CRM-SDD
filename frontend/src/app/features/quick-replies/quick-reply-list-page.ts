import { Clipboard } from '@angular/cdk/clipboard';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, debounceTime, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { AuthService } from '../../core/auth/auth-service';
import { I18n } from '../../core/i18n/i18n';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { TICKET_CATEGORIES, TicketCategory } from '../tickets/ticket-models';
import { copyQuickReply } from './copy-quick-reply';
import { QuickReply, QuickReplyListParams, QuickReplyScope } from './quick-reply-models';
import { QuickRepliesApi } from './quick-replies-api';

@Component({
  selector: 'crm-quick-reply-list-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    TranslatePipe,
    MatCardModule,
    EmptyState,
    ErrorState,
    StatusBadge,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './quick-reply-list-page.html',
  styleUrl: './quick-reply-list-page.scss',
})
export class QuickReplyListPage {
  private readonly i18n = inject(I18n);
  private readonly api = inject(QuickRepliesApi);
  private readonly auth = inject(AuthService);
  private readonly clipboard = inject(Clipboard);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<QuickReplyListParams>();

  protected readonly isSuperAdmin = computed(() => this.auth.role() === 'SuperAdmin');
  protected readonly categories = TICKET_CATEGORIES;
  protected readonly pageSizeOptions = [10, 20, 50];
  protected readonly filters = inject(NonNullableFormBuilder).group({
    search: [''],
    category: [null as TicketCategory | null],
    scope: [null as QuickReplyScope | null],
    archived: [false],
  });
  protected readonly items = signal<QuickReply[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.requests
      .pipe(
        switchMap(params =>
          this.api.list(params).pipe(
            catchError((error: HttpErrorResponse) => {
              this.error.set(describeHttpError(error, this.i18n.t('quickReplies.list.loadFailed'), this.i18n.t));
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

    this.filters.valueChanges.pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load(1));
    this.load(1);
  }

  protected copy(item: QuickReply): void {
    copyQuickReply(this.clipboard, this.snackBar, item.body, this.i18n.t);
  }

  protected setArchived(item: QuickReply, archived: boolean): void {
    (archived ? this.api.archive(item.id) : this.api.restore(item.id)).subscribe({
      next: () => this.load(this.page()),
      error: (error: HttpErrorResponse) => this.error.set(describeHttpError(error, this.i18n.t('quickReplies.list.changeFailed'), this.i18n.t)),
    });
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
    this.requests.next({
      page,
      pageSize: this.pageSize(),
      search: f.search,
      category: f.category,
      scope: this.isSuperAdmin() ? null : f.scope,
      archived: f.archived,
    });
  }
}
