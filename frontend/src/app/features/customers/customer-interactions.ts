import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { EMPTY, Subject, catchError, map, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { fromLocalInput, toLocalInput } from '../tasks/task-models';
import { CustomerRecordsApi } from './customer-records-api';
import {
  CUSTOMER_INTERACTION_TYPES,
  CustomerInteraction,
  CustomerInteractionType,
  describeFieldError,
  interactionTypeLabel,
} from './customer-records-models';

const PAGE_SIZE = 10;
const FUTURE_TOLERANCE_MS = 5 * 60_000; // same clock-skew tolerance as the API

/** Manually logged, append-only customer interactions. Plain text only, always rendered with interpolation. */
@Component({
  selector: 'crm-customer-interactions',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    StatusBadge,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
  ],
  templateUrl: './customer-interactions.html',
  styleUrl: './customer-records.scss',
})
export class CustomerInteractions {
  private readonly api = inject(CustomerRecordsApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<{ id: string; page: number }>();
  private nextPage = 1;

  readonly customerId = input.required<string>();

  protected readonly types = CUSTOMER_INTERACTION_TYPES;
  protected readonly typeLabel = interactionTypeLabel;
  protected readonly form = inject(NonNullableFormBuilder).group({
    type: ['PhoneCall' as CustomerInteractionType, Validators.required],
    summary: ['', [Validators.required, Validators.maxLength(500)]],
    occurredAt: [toLocalInput(new Date().toISOString()), Validators.required],
  });
  protected readonly items = signal<CustomerInteraction[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(false);
  protected readonly loaded = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly saveError = signal<string | null>(null);
  protected readonly hasMore = computed(() => this.items().length < this.totalCount());

  constructor() {
    this.requests
      .pipe(
        switchMap(r =>
          this.api.interactions(r.id, r.page, PAGE_SIZE).pipe(
            map(result => ({ r, result })),
            catchError((error: HttpErrorResponse) => {
              this.loading.set(false);
              this.error.set(describeHttpError(error, 'Could not load interactions.'));
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
          const known = new Set(this.items().map(i => i.id)); // items added meanwhile shift offsets
          this.items.update(current => [...current, ...result.items.filter(i => !known.has(i.id))]);
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

  protected save(): void {
    const { type, summary, occurredAt } = this.form.getRawValue();
    if (this.form.invalid || !summary.trim() || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }
    const occurredAtUtc = fromLocalInput(occurredAt);
    if (!occurredAtUtc || new Date(occurredAtUtc).getTime() > Date.now() + FUTURE_TOLERANCE_MS) {
      this.saveError.set('The interaction time cannot be in the future.');
      return;
    }
    this.saving.set(true);
    this.saveError.set(null);
    this.api.logInteraction(this.customerId(), { type, summary: summary.trim(), occurredAtUtc }).subscribe({
      next: () => {
        this.saving.set(false);
        this.form.patchValue({ summary: '', occurredAt: toLocalInput(new Date().toISOString()) });
        this.form.controls.summary.markAsUntouched();
        this.snackBar.open('Interaction saved.', 'OK', { duration: 3000 });
        // A back-dated entry belongs further down the list: reload page 1 so order and paging stay correct.
        this.load(this.customerId(), 1);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        const problems = (error.error as { errors?: Record<string, string[]> } | null)?.errors;
        const field = ['occurredAtUtc', 'summary', 'type'].find(f => problems?.[f]?.length);
        this.saveError.set(field ? describeFieldError(error, field, '') : describeHttpError(error, 'Could not save the interaction.'));
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
