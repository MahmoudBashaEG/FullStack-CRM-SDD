import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/describe-http-error';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { CustomerAttachments } from './customer-attachments';
import { CustomerInteractions } from './customer-interactions';
import { Customer } from './customer-models';
import { CustomerNotes } from './customer-notes';
import { CustomerRelatedTickets } from './customer-related-tickets';
import { CustomersApi } from './customers-api';

@Component({
  selector: 'crm-customer-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DatePipe,
    PageHeader,
    StatusBadge,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    CustomerNotes,
    CustomerInteractions,
    CustomerRelatedTickets,
    CustomerAttachments,
  ],
  templateUrl: './customer-detail-page.html',
  styleUrl: './customer-detail-page.scss',
})
export class CustomerDetailPage {
  private readonly api = inject(CustomersApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';

  protected readonly customer = signal<Customer | null>(null);
  protected readonly loadError = signal<string | null>(null);

  protected readonly form = this.fb.group({
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    phoneNumber: ['', [Validators.maxLength(32)]],
    company: ['', [Validators.maxLength(200)]],
  });
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  constructor() {
    this.api
      .get(this.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: customer => this.show(customer),
        error: (error: HttpErrorResponse) =>
          this.loadError.set(describeHttpError(error, 'Could not load the customer.')),
      });
  }

  private show(customer: Customer): void {
    this.customer.set(customer);
    this.form.reset({
      firstName: customer.firstName,
      lastName: customer.lastName,
      email: customer.email,
      phoneNumber: customer.phoneNumber ?? '',
      company: customer.company ?? '',
    });
    // A registered customer's email is their login email: read-only here (the API also rejects a change).
    if (customer.isRegistered) {
      this.form.controls.email.disable();
    } else {
      this.form.controls.email.enable();
    }
  }

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    // getRawValue() includes the disabled email, so a registered customer's unchanged email is sent.
    const value = this.form.getRawValue();
    this.saving.set(true);
    this.saveError.set(null);
    this.api
      .update(this.id, {
        firstName: value.firstName.trim(),
        lastName: value.lastName.trim(),
        email: value.email.trim(),
        phoneNumber: value.phoneNumber.trim() || null,
        company: value.company.trim() || null,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: updated => {
          this.show(updated);
          this.saving.set(false);
          this.snackBar.open('Customer updated.', undefined, { duration: 3000 });
        },
        error: (error: HttpErrorResponse) => {
          this.saveError.set(describeHttpError(error, 'Could not update the customer.'));
          this.saving.set(false);
        },
      });
  }
}
