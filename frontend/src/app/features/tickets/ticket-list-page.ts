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
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, debounceTime, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { AuthService } from '../../core/auth/auth-service';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { Customer } from '../customers/customer-models';
import { CustomerPicker } from './customer-picker';
import {
  TICKET_CATEGORIES,
  TICKET_PRIORITIES,
  TICKET_STATUSES,
  Ticket,
  TicketCategory,
  TicketListParams,
  TicketPriority,
  TicketStatus,
  priorityTone,
  statusLabel,
  statusTone,
} from './ticket-models';
import { TicketsApi } from './tickets-api';
import { slaIndicatorTone } from '../sla/sla-models';

@Component({
  selector: 'crm-ticket-list-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    LocalizedDatePipe,
    TranslatePipe,
    EmptyState,
    ErrorState,
    MatCardModule,
    PageHeader,
    StatusBadge,
    CustomerPicker,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './ticket-list-page.html',
  styleUrl: './ticket-list-page.scss',
})
export class TicketListPage {
  private readonly api = inject(TicketsApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<TicketListParams>();
  private readonly auth = inject(AuthService);
  protected readonly i18n = inject(I18n);

  protected readonly isSuperAdmin = computed(() => this.auth.role() === 'SuperAdmin'); // wording only; the API scopes the data
  protected readonly columns = ['subject', 'customer', 'category', 'priority', 'status', 'sla', 'assignee', 'created', 'actions'];
  protected readonly pageSizeOptions = [10, 20, 50, 100];
  protected readonly categories = TICKET_CATEGORIES;
  protected readonly priorities = TICKET_PRIORITIES;
  protected readonly statuses = TICKET_STATUSES;
  protected readonly priorityTone = priorityTone;
  protected readonly statusLabel = statusLabel;
  protected readonly statusTone = statusTone;
  protected readonly slaIndicatorTone = slaIndicatorTone;
  protected readonly filters = inject(NonNullableFormBuilder).group({
    search: [''],
    status: [null as TicketStatus | null],
    category: [null as TicketCategory | null],
    priority: [null as TicketPriority | null],
    escalated: [null as boolean | null],
    slaBreached: [null as boolean | null],
  });
  protected readonly customerId = signal<string | null>(null);
  protected readonly tickets = signal<Ticket[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1); // one-based, as the API
  protected readonly pageSize = signal(20);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.requests
      .pipe(
        // A newer search/page cancels the older request. Errors are caught per request (inside switchMap),
        // so a failed request does not complete the outer stream and later searches/pages still load.
        switchMap(params =>
          this.api.list(params).pipe(
            catchError((error: HttpErrorResponse) => {
              this.error.set(describeHttpError(error, this.i18n.t('tickets.list.loadError'), this.i18n.t));
              this.loading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(result => {
        this.tickets.set(result.items);
        this.totalCount.set(result.totalCount);
        this.page.set(result.page);
        this.pageSize.set(result.pageSize);
        this.loading.set(false);
      });

    this.filters.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load(1));

    this.load(1);
  }

  protected onCustomer(customer: Customer | null): void {
    const id = customer?.id ?? null;
    if (id !== this.customerId()) {
      this.customerId.set(id);
      this.load(1);
    }
  }

  protected hasFilters(): boolean {
    const f = this.filters.getRawValue();
    return !!(f.search.trim() || f.status || f.category || f.priority || f.escalated !== null || f.slaBreached !== null || this.customerId());
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
      customerId: this.customerId(),
      status: f.status,
      category: f.category,
      priority: f.priority,
      escalated: f.escalated,
      slaBreached: f.slaBreached,
    });
  }
}
