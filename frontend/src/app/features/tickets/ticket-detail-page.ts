import { DatePipe } from '@angular/common';
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
import { ActivatedRoute, RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/describe-http-error';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { TICKET_CATEGORIES, TICKET_PRIORITIES, Ticket, TicketCategory, TicketPriority, priorityTone } from './ticket-models';
import { TicketsApi } from './tickets-api';

@Component({
  selector: 'crm-ticket-detail-page',
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
    MatSelectModule,
    MatButtonModule,
  ],
  templateUrl: './ticket-detail-page.html',
  styleUrl: './ticket-detail-page.scss',
})
export class TicketDetailPage {
  private readonly api = inject(TicketsApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';

  protected readonly categories = TICKET_CATEGORIES;
  protected readonly priorities = TICKET_PRIORITIES;
  protected readonly priorityTone = priorityTone;
  protected readonly ticket = signal<Ticket | null>(null);
  protected readonly loadError = signal<string | null>(null);

  protected readonly form = this.fb.group({
    subject: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(4000)]],
    category: this.fb.control<TicketCategory>('General', Validators.required),
    priority: this.fb.control<TicketPriority>('Medium', Validators.required),
  });
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  constructor() {
    this.api
      .get(this.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ticket => this.show(ticket),
        error: (error: HttpErrorResponse) =>
          this.loadError.set(describeHttpError(error, 'Could not load the ticket.')),
      });
  }

  private show(ticket: Ticket): void {
    this.ticket.set(ticket);
    this.form.reset({
      subject: ticket.subject,
      description: ticket.description,
      category: ticket.category,
      priority: ticket.priority,
    });
  }

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    this.saveError.set(null);
    this.api
      .update(this.id, {
        subject: value.subject.trim(),
        description: value.description.trim(),
        category: value.category,
        priority: value.priority,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: updated => {
          this.show(updated);
          this.saving.set(false);
          this.snackBar.open('Ticket updated.', undefined, { duration: 3000 });
        },
        error: (error: HttpErrorResponse) => {
          this.saveError.set(describeHttpError(error, 'Could not update the ticket.'));
          this.saving.set(false);
        },
      });
  }
}
