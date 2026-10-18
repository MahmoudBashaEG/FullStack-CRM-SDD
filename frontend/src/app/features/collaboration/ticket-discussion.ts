import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
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
import { CollaborationApi } from './collaboration-api';
import { TicketMessage } from './collaboration-models';

const PAGE_SIZE = 20;

/** Append-only internal staff discussion for one ticket. Plain text only, always rendered with interpolation. */
@Component({
  selector: 'crm-ticket-discussion',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressBarModule],
  templateUrl: './ticket-discussion.html',
  styleUrl: './ticket-discussion.scss',
})
export class TicketDiscussion {
  private readonly api = inject(CollaborationApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<{ id: string; page: number }>();
  private nextPage = 1;

  readonly ticketId = input.required<string>();
  /** The caller lost access (404): the parent decides what to show. */
  readonly accessLost = output<void>();

  protected readonly composer = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(2000)] });
  protected readonly items = signal<TicketMessage[]>([]);
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
          this.api.messages(r.id, r.page, PAGE_SIZE).pipe(
            map(result => ({ r, result })),
            catchError((error: HttpErrorResponse) => {
              this.loading.set(false);
              if (error.status === 404) {
                this.accessLost.emit();
              } else {
                this.error.set(describeHttpError(error, 'Could not load the discussion.'));
              }
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
          const known = new Set(this.items().map(m => m.id)); // messages posted meanwhile shift offsets
          this.items.update(current => [...current, ...result.items.filter(m => !known.has(m.id))]);
        }
        this.totalCount.set(result.totalCount);
        this.nextPage = r.page + 1;
        this.loaded.set(true);
        this.loading.set(false);
      });

    effect(() => {
      const id = this.ticketId();
      untracked(() => this.load(id, 1));
    });
  }

  protected post(): void {
    const text = this.composer.value.trim();
    if (!text || this.composer.invalid || this.posting()) {
      this.composer.markAsTouched();
      return;
    }
    this.posting.set(true);
    this.postError.set(null);
    this.api.post(this.ticketId(), text).subscribe({
      next: message => {
        this.posting.set(false);
        this.items.update(current => [message, ...current.filter(m => m.id !== message.id)]);
        this.totalCount.update(n => n + 1);
        this.composer.reset('');
        this.snackBar.open('Message posted.', 'OK', { duration: 3000 });
      },
      error: (error: HttpErrorResponse) => {
        this.posting.set(false);
        if (error.status === 404) {
          this.accessLost.emit();
        } else if (error.status === 503) {
          this.postError.set('Your message may have been posted. Check the thread before posting again.');
          this.load(this.ticketId(), 1); // keep the typed text
        } else {
          this.postError.set(describeHttpError(error, 'Could not post the message.'));
        }
      },
    });
  }

  protected loadMore(): void {
    this.load(this.ticketId(), this.nextPage);
  }

  protected retry(): void {
    this.load(this.ticketId(), this.items().length === 0 ? 1 : this.nextPage);
  }

  private load(id: string, page: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.requests.next({ id, page });
  }
}
