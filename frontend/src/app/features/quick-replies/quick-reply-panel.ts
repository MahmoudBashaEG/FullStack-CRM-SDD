import { Clipboard } from '@angular/cdk/clipboard';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { EMPTY, catchError, debounceTime, startWith, switchMap, tap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { copyQuickReply } from './copy-quick-reply';
import { QUICK_REPLY_COPY_HINT, QuickReply } from './quick-reply-models';
import { QuickRepliesApi } from './quick-replies-api';

/** Compact searchable list for Ticket Details. Copy only — nothing is sent to the customer. */
@Component({
  selector: 'crm-quick-reply-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, MatButtonModule, MatFormFieldModule, MatInputModule, MatIconModule],
  templateUrl: './quick-reply-panel.html',
  styleUrl: '../../shared/ui/productivity-page.scss',
})
export class QuickReplyPanel {
  private readonly api = inject(QuickRepliesApi);
  private readonly clipboard = inject(Clipboard);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly hint = QUICK_REPLY_COPY_HINT;
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly items = signal<QuickReply[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly destroyRef = inject(DestroyRef);

  constructor() {
    this.search.valueChanges
      .pipe(
        startWith(''),
        debounceTime(300),
        tap(() => this.error.set(null)),
        switchMap(term =>
          this.api.list({ page: 1, pageSize: 5, search: term, category: null, scope: null, archived: false }).pipe(
            catchError((error: HttpErrorResponse) => {
              this.error.set(describeHttpError(error, 'Could not load quick replies.'));
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(result => this.items.set(result.items));
  }

  protected copy(item: QuickReply): void {
    copyQuickReply(this.clipboard, this.snackBar, item.body);
  }
}
