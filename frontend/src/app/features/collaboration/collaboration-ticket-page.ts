import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/describe-http-error';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { priorityTone, statusLabel, statusTone } from '../tickets/ticket-models';
import { CollaborationApi } from './collaboration-api';
import { CollaborationTicket } from './collaboration-models';
import { TicketCollaborators } from './ticket-collaborators';
import { TicketDiscussion } from './ticket-discussion';

/** Invited collaborators only see minimal context here — never the ticket details, customer or activity. */
@Component({
  selector: 'crm-collaboration-ticket-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, PageHeader, StatusBadge, TicketDiscussion, TicketCollaborators, MatCardModule],
  templateUrl: './collaboration-ticket-page.html',
  styleUrl: '../../shared/ui/productivity-page.scss',
})
export class CollaborationTicketPage {
  private readonly api = inject(CollaborationApi);

  protected readonly ticketId = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  protected readonly priorityTone = priorityTone;
  protected readonly statusLabel = statusLabel;
  protected readonly statusTone = statusTone;
  protected readonly ticket = signal<CollaborationTicket | null>(null);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.api.ticket(this.ticketId).subscribe({
      next: t => this.ticket.set(t),
      error: (error: HttpErrorResponse) => this.setError(error),
    });
  }

  protected onAccessLost(): void {
    this.ticket.set(null);
    this.error.set('This discussion is no longer available to you.');
  }

  private setError(error: HttpErrorResponse): void {
    this.ticket.set(null);
    this.error.set(error.status === 404 ? 'This discussion is no longer available to you.' : describeHttpError(error, 'Could not load the discussion.'));
  }
}
