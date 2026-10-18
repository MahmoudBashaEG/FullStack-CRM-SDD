import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { EMPTY, Subject, catchError, map, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { CustomerRecordsApi } from './customer-records-api';
import { CustomerNote, describeFieldError } from './customer-records-models';

const PAGE_SIZE = 10;

/** Append-only internal staff notes for one customer. Plain text only, always rendered with interpolation. */
@Component({
  selector: 'crm-customer-notes',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizedDatePipe, TranslatePipe, EmptyState, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressBarModule],
  templateUrl: './customer-notes.html',
  styleUrl: './customer-records.scss',
})
export class CustomerNotes {
  private readonly i18n = inject(I18n);
  private readonly api = inject(CustomerRecordsApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<{ id: string; page: number }>();
  private nextPage = 1;

  readonly customerId = input.required<string>();

  protected readonly composer = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(2000)] });
  protected readonly items = signal<CustomerNote[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(false);
  protected readonly loaded = signal(false);
  protected readonly posting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly postError = signal<string | null>(null);
  protected readonly hasMore = computed(() => this.items().length < this.totalCount());

  constructor() {
    this.requests
      .pipe(
        switchMap(r =>
          this.api.notes(r.id, r.page, PAGE_SIZE).pipe(
            map(result => ({ r, result })),
            catchError((error: HttpErrorResponse) => {
              this.loading.set(false);
              this.error.set(describeHttpError(error, this.i18n.t('customers.notes.loadFailed'), this.i18n.t));
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(({ r, result }) => {
        if (r.page === 1) {
          this.items.set(result.items);
        } else {
          const known = new Set(this.items().map(n => n.id)); // notes added meanwhile shift offsets
          this.items.update(current => [...current, ...result.items.filter(n => !known.has(n.id))]);
        }
        this.totalCount.set(result.totalCount);
        this.nextPage = r.page + 1;
        this.loaded.set(true);
        this.loading.set(false);
      });

    effect(() => {
      const id = this.customerId();
      untracked(() => this.load(id, 1));
    });
  }

  protected add(): void {
    const text = this.composer.value.trim();
    if (!text || this.composer.invalid || this.posting()) {
      this.composer.markAsTouched();
      return;
    }
    this.posting.set(true);
    this.postError.set(null);
    this.api.addNote(this.customerId(), text).subscribe({
      next: note => {
        this.posting.set(false);
        this.items.update(current => [note, ...current.filter(n => n.id !== note.id)]);
        this.totalCount.update(n => n + 1);
        this.composer.reset('');
        this.snackBar.open(this.i18n.t('customers.notes.added'), 'OK', { duration: 3000 });
      },
      error: (error: HttpErrorResponse) => {
        this.posting.set(false);
        this.postError.set(describeFieldError(error, 'body', this.i18n.t('customers.notes.addFailed'), this.i18n.t));
      },
    });
  }

  protected loadMore(): void {
    this.load(this.customerId(), this.nextPage);
  }

  protected retry(): void {
    this.load(this.customerId(), this.items().length === 0 ? 1 : this.nextPage);
  }

  private load(id: string, page: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.requests.next({ id, page });
  }
}
