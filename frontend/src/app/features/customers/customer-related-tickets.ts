import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, map, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { Ticket, priorityTone, statusLabel, statusTone } from '../tickets/ticket-models';
import { TicketsApi } from '../tickets/tickets-api';

const PAGE_SIZE = 10;

/** The customer's tickets the current staff member may already open (the ticket list scopes Agents to their own). */
@Component({
  selector: 'crm-customer-related-tickets',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, RouterLink, StatusBadge, MatButtonModule, MatProgressBarModule],
  templateUrl: './customer-related-tickets.html',
  styleUrl: './customer-records.scss',
})
export class CustomerRelatedTickets {
  private readonly api = inject(TicketsApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<{ id: string; page: number }>();
  private nextPage = 1;

  readonly customerId = input.required<string>();

  protected readonly statusLabel = statusLabel;
  protected readonly statusTone = statusTone;
  protected readonly priorityTone = priorityTone;
  protected readonly items = signal<Ticket[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(false);
  protected readonly loaded = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly hasMore = computed(() => this.items().length < this.totalCount());

  constructor() {
    this.requests
      .pipe(
        switchMap(r =>
          this.api
            .list({ page: r.page, pageSize: PAGE_SIZE, search: null, customerId: r.id, status: null, category: null, priority: null })
            .pipe(
              map(result => ({ r, result })),
              catchError((error: HttpErrorResponse) => {
                this.loading.set(false);
                this.error.set(describeHttpError(error, 'Could not load related tickets.'));
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
          const known = new Set(this.items().map(t => t.id));
          this.items.update(current => [...current, ...result.items.filter(t => !known.has(t.id))]);
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
