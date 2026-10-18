import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/describe-http-error';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { TICKET_CATEGORIES, TicketCategory } from '../tickets/ticket-models';
import { PortalApi } from './portal-api';
import { NOT_LINKED, PORTAL_LIMITS } from './portal-models';

@Component({
  selector: 'crm-portal-ticket-create-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
  ],
  templateUrl: './portal-ticket-create-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './portal-page.scss'],
})
export class PortalTicketCreatePage {
  private readonly api = inject(PortalApi);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly categories = TICKET_CATEGORIES;
  protected readonly limits = PORTAL_LIMITS;
  protected readonly form = this.fb.group({
    subject: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(PORTAL_LIMITS.subject)]],
    description: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(PORTAL_LIMITS.description)]],
    category: this.fb.control<TicketCategory | null>(null, Validators.required),
  });
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected submit(): void {
    if (this.form.invalid || this.submitting()) { // prevents duplicate submits while a request is pending
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    this.submitting.set(true);
    this.error.set(null);
    this.api
      .submitTicket({ subject: v.subject.trim(), description: v.description.trim(), category: v.category! })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ticket => {
          this.snackBar.open('Request submitted. Our team will review it.', undefined, { duration: 4000 });
          void this.router.navigate(['/portal/tickets', ticket.id]);
        },
        error: (error: HttpErrorResponse) => {
          this.error.set(error.status === 404 ? NOT_LINKED : describeHttpError(error, 'Could not submit your request.'));
          this.submitting.set(false);
        },
      });
  }
}
