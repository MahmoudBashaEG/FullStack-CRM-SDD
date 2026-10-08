import { DestroyRef, inject, Injectable, signal, computed } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject } from 'rxjs';
import { TicketsService } from '../data-access/tickets.service';
import { TicketDetail, TicketDetailsPatch, TicketStatus } from '../models/ticket.models';

@Injectable()
export class TicketEditSession {
  private generation = 0;
  private readonly service = inject(TicketsService); private readonly destroy = inject(DestroyRef);
  readonly detail = signal<TicketDetail | null>(null); readonly seed = signal<TicketDetail | null>(null);
  readonly busy = signal(false); readonly needsReload = signal(false); readonly review = signal(false); readonly reviewed = signal(false);
  readonly message = signal(''); readonly fieldErrors = signal<Record<string, string[]>>({});
  readonly blocked = computed(() => this.busy() || this.needsReload() || this.review() && !this.reviewed() || this.detail()?.status === 'Closed');
  readonly statusBlocked = computed(() => this.busy() || this.needsReload() || this.review() && !this.reviewed());
  saveStatus(status: TicketStatus): void { const ticket = this.detail(); if (!ticket || this.statusBlocked()) return; this.save(this.service.changeStatus(ticket.reference, ticket.editVersion, status)); }
  readonly saved = new Subject<TicketDetail>();
  reset(): void { this.generation++; this.detail.set(null); this.seed.set(null); this.busy.set(false); this.needsReload.set(false); this.review.set(false); this.reviewed.set(false); this.message.set(''); this.fieldErrors.set({}); }
  accept(detail: TicketDetail): void { this.generation++; this.detail.set(detail); this.seed.set(detail); this.needsReload.set(false); this.review.set(false); this.reviewed.set(false); this.message.set(''); this.fieldErrors.set({}); }
  saveDetails(patch: TicketDetailsPatch): void { const ticket = this.detail(); if (!ticket || this.blocked()) return; this.save(this.service.update(ticket.reference, ticket.editVersion, patch)); }
  saveAssignment(agentId: string | null): void { const ticket = this.detail(); if (!ticket || this.blocked()) return; this.save(this.service.assign(ticket.reference, ticket.editVersion, agentId)); }
  reload(): void {
    const ticket = this.detail(); if (!ticket || this.busy()) return;
    const generation = this.generation;
    this.busy.set(true);
    this.service.detail(ticket.reference).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: latest => {
      if (generation !== this.generation) return;
      this.detail.set(latest); this.needsReload.set(false); this.review.set(true); this.reviewed.set(false); this.busy.set(false);
      this.message.set('Latest saved information is shown above. Review it alongside your preserved draft before resubmitting.');
    }, error: () => { if (generation !== this.generation) return; this.busy.set(false); this.message.set('Latest information could not be loaded. Your draft is preserved. Retry reload.'); } });
  }
  confirmReview(): void { if (this.review() && !this.needsReload()) this.reviewed.set(true); }
  private save(request: ReturnType<TicketsService['update']>): void {
    const generation = this.generation;
    this.busy.set(true); this.message.set(''); this.fieldErrors.set({});
    request.pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: detail => { if (generation !== this.generation) return; this.busy.set(false); this.accept(detail); this.saved.next(detail); }, error: (error: HttpErrorResponse) => {
      if (generation !== this.generation) return;
      this.busy.set(false); this.fieldErrors.set(error.error?.errors ?? {});
      const code = error.error?.code;
      if (code === 'ticket_changed' || error.status === 0 || error.status === 503) {
        this.needsReload.set(true); this.review.set(false); this.reviewed.set(false);
        this.message.set(code === 'ticket_changed' ? 'Another staff member changed this ticket. Your draft is preserved. Reload and review before resubmitting.' : 'The save outcome is uncertain. Your draft is preserved. Reload and review before deliberately retrying.');
      } else this.message.set(code === 'invalid_transition' ? 'That status change is not allowed from the current ticket status. Review the ticket and choose an allowed status.' : code === 'category_unavailable' ? 'That new category is unavailable. Choose an available category.' : code === 'agent_ineligible' ? 'That new agent is ineligible. Choose an eligible agent or unassign.' : code === 'ticket_closed' ? 'This ticket is closed. Edits and assignment are unavailable until it is reopened.' : error.status === 400 ? 'Check the ticket values and try again.' : error.status === 403 ? 'You do not have shared queue access.' : error.status === 401 ? 'Sign in to access the support queue.' : error.status === 404 ? 'Ticket not found.' : 'The change could not be saved. Your draft is preserved.');
    } });
  }
}
