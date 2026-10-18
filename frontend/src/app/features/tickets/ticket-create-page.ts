import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/describe-http-error';
import { AuthService } from '../../core/auth/auth-service';
import { I18n } from '../../core/i18n/i18n';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { Agent } from '../agents/agent-models';
import { AgentsApi } from '../agents/agents-api';
import { Customer } from '../customers/customer-models';
import { CustomerPicker } from './customer-picker';
import { TICKET_CATEGORIES, TICKET_PRIORITIES, TicketCategory, TicketPriority } from './ticket-models';
import { TicketsApi } from './tickets-api';

@Component({
  selector: 'crm-ticket-create-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    StatusBadge,
    TranslatePipe,
    CustomerPicker,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
  ],
  templateUrl: './ticket-create-page.html',
  styleUrl: './ticket-create-page.scss',
})
export class TicketCreatePage {
  private readonly api = inject(TicketsApi);
  private readonly agentsApi = inject(AgentsApi);
  private readonly auth = inject(AuthService);
  private readonly i18n = inject(I18n);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly isSuperAdmin = computed(() => this.auth.role() === 'SuperAdmin');
  protected readonly categories = TICKET_CATEGORIES;
  protected readonly priorities = TICKET_PRIORITIES;
  protected readonly form = this.fb.group({
    customerId: this.fb.control<string | null>(null, Validators.required),
    subject: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(4000)]],
    category: this.fb.control<TicketCategory | null>(null, Validators.required),
    priority: this.fb.control<TicketPriority>('Medium', Validators.required),
    assignedAgentUserId: this.fb.control<string | null>(null), // SuperAdmin only; null = Unassigned
  });
  protected readonly activeAgents = signal<Agent[]>([]);
  protected readonly agentsError = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    // Agents never call /api/v1/agents (SuperAdmin-only endpoint); the server re-validates the choice anyway.
    if (this.isSuperAdmin()) {
      this.agentsApi
        .list()
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: agents => this.activeAgents.set(agents.filter(a => a.isActive)),
          error: (error: HttpErrorResponse) =>
            this.agentsError.set(describeHttpError(error, this.i18n.t('tickets.create.agentsError'), this.i18n.t) + ' ' + this.i18n.t('tickets.create.agentsErrorSuffix')),
        });
    }
  }

  protected onCustomer(customer: Customer | null): void {
    this.form.controls.customerId.setValue(customer?.id ?? null);
    this.form.controls.customerId.markAsTouched();
  }

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.submitting.set(true);
    this.error.set(null);
    this.api
      .create({
        customerId: value.customerId!,
        subject: value.subject.trim(),
        description: value.description.trim(),
        category: value.category!,
        priority: value.priority,
        // Agents never send an assignee: the server assigns the ticket to them.
        assignedAgentUserId: this.isSuperAdmin() ? value.assignedAgentUserId : null,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ticket => {
          this.snackBar.open(this.i18n.t('tickets.create.created'), undefined, { duration: 3000 });
          void this.router.navigate(['/tickets', ticket.id]);
        },
        error: (error: HttpErrorResponse) => {
          this.error.set(describeHttpError(error, this.i18n.t('tickets.create.createError'), this.i18n.t));
          this.submitting.set(false);
        },
      });
  }
}
