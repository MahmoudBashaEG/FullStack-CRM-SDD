import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, output, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { describeHttpError } from '../../core/api/describe-http-error';
import { PortalApi } from './portal-api';
import { PORTAL_LIMITS, PortalFeedback, PortalTicket } from './portal-models';

/** CSAT card (CP5): one editable rating per ticket. The API re-checks ownership and that the ticket is Resolved/Closed. */
@Component({
  selector: 'crm-portal-ticket-feedback',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    DatePipe,
    MatCardModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
  ],
  templateUrl: './portal-ticket-feedback.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './portal-page.scss'],
})
export class PortalTicketFeedback {
  private readonly api = inject(PortalApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  readonly ticketId = input.required<string>();
  readonly canSubmit = input.required<boolean>();
  readonly feedback = input<PortalFeedback | null>(null);
  readonly saved = output<PortalTicket>();

  protected readonly ratings = [1, 2, 3, 4, 5];
  protected readonly commentMax = PORTAL_LIMITS.feedbackComment;
  protected readonly editing = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    rating: [null as number | null, [Validators.required, Validators.min(1), Validators.max(5)]],
    comment: ['', Validators.maxLength(PORTAL_LIMITS.feedbackComment)],
  });

  constructor() {
    // Reset whenever the ticket or its saved feedback changes (no stale form across tickets).
    effect(() => {
      const f = this.feedback();
      this.ticketId();
      untracked(() => {
        this.form.reset({ rating: f?.rating ?? null, comment: f?.comment ?? '' });
        this.editing.set(false);
        this.error.set(null);
      });
    });
  }

  protected startEdit(): void {
    this.error.set(null);
    this.editing.set(true);
  }

  protected cancelEdit(): void {
    const f = this.feedback();
    this.form.reset({ rating: f?.rating ?? null, comment: f?.comment ?? '' });
    this.error.set(null);
    this.editing.set(false);
  }

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.api
      .saveFeedback(this.ticketId(), { rating: v.rating!, comment: v.comment.trim() || null })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: updated => {
          this.snackBar.open('Thank you for your feedback.', undefined, { duration: 3000 });
          this.saving.set(false);
          this.editing.set(false);
          this.saved.emit(updated);
        },
        error: (error: HttpErrorResponse) => {
          // 409 (race / overlapping edit / ticket reopened): show the server's message; reloading the page shows the stored state.
          this.error.set(describeHttpError(error, 'Could not save your feedback.'));
          this.saving.set(false);
        },
      });
  }
}
