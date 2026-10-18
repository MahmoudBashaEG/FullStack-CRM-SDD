import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/describe-http-error';
import { I18n } from '../../core/i18n/i18n';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { priorityTone, statusTone } from '../tickets/ticket-models';
import { CollaborationApi } from './collaboration-api';
import { CollaborationTicket } from './collaboration-models';
import { TicketCollaborators } from './ticket-collaborators';
import { TicketDiscussion } from './ticket-discussion';

/** Invited collaborators only see minimal context here — never the ticket details, customer or activity. */
@Component({
  selector: 'crm-collaboration-ticket-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, ErrorState, PageHeader, StatusBadge, TicketDiscussion, TicketCollaborators, MatButtonModule, MatCardModule, MatIconModule],
  templateUrl: './collaboration-ticket-page.html',
  styleUrl: '../../shared/ui/productivity-page.scss',
})
export class CollaborationTicketPage {
  private readonly api = inject(CollaborationApi);
  private readonly i18n = inject(I18n);

  protected readonly ticketId = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  protected readonly priorityTone = priorityTone;
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
    this.error.set(this.i18n.t('collaboration.unavailable'));
  }

  private setError(error: HttpErrorResponse): void {
    this.ticket.set(null);
    this.error.set(error.status === 404 ? this.i18n.t('collaboration.unavailable') : describeHttpError(error, this.i18n.t('collaboration.loadError'), this.i18n.t));
  }
}
