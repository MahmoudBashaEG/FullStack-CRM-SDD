import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { TicketsApi } from '../tickets/tickets-api';
import { AiApi } from './ai-api';
import { chatRequestMessages } from './ai-chat';
import { AiChatStore } from './ai-chat-store';
import { describeAiError } from './ai-errors';
import { AI_CHAT_MESSAGE_MAX_LENGTH, AI_REVIEW_LABEL } from './ai-models';

/** AI-001 staff assistant. History lives in AiChatStore (browser memory only); ticket access is re-checked by the server every turn. */
@Component({
  selector: 'crm-ai-assistant-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
  ],
  templateUrl: './ai-assistant-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './ai-assistant-page.scss'],
})
export class AiAssistantPage {
  private readonly route = inject(ActivatedRoute);
  private readonly ticketsApi = inject(TicketsApi);
  private readonly ai = inject(AiApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly ticketChanged = new Subject<void>();

  protected readonly store = inject(AiChatStore);
  protected readonly reviewLabel = AI_REVIEW_LABEL;
  protected readonly maxLength = AI_CHAT_MESSAGE_MAX_LENGTH;
  protected readonly message = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(AI_CHAT_MESSAGE_MAX_LENGTH)],
  });
  protected readonly ticketSubject = signal<string | null>(null);
  protected readonly ticketLost = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => this.openTicket(params.get('ticketId')));
  }

  protected send(): void {
    const text = this.message.value.trim();
    if (!text || this.busy() || this.ticketLost()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.ai
      .chat({ ticketId: this.store.ticketId(), messages: chatRequestMessages(this.store.turns(), text) })
      .pipe(takeUntil(this.ticketChanged), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: reply => {
          this.busy.set(false);
          this.store.append({ role: 'User', text, sources: [] }, { role: 'Assistant', text: reply.reply, sources: reply.sources });
          this.message.reset('');
        },
        error: (e: HttpErrorResponse) => {
          this.busy.set(false);
          if (e.status === 404 && this.store.ticketId()) {
            this.loseTicket();
            return;
          }
          this.error.set(describeAiError(e, 'The assistant could not answer.')); // the typed text stays for Retry
        },
      });
  }

  protected newConversation(): void {
    this.store.clear();
    this.error.set(null);
  }

  private openTicket(ticketId: string | null): void {
    this.ticketChanged.next(); // drop an in-flight answer that belongs to the previous ticket
    this.busy.set(false);
    this.error.set(null);
    this.ticketSubject.set(null);
    this.ticketLost.set(false);
    this.store.open(ticketId);
    if (!ticketId) {
      return;
    }
    this.ticketsApi
      .get(ticketId)
      .pipe(takeUntil(this.ticketChanged), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ticket => this.ticketSubject.set(ticket.subject), // from the authorized API, never from the URL
        error: (e: HttpErrorResponse) => {
          if (e.status === 404) {
            this.loseTicket();
          } else {
            this.error.set(describeAiError(e, 'Could not load the ticket.'));
          }
        },
      });
  }

  private loseTicket(): void {
    this.store.reset();
    this.ticketSubject.set(null);
    this.ticketLost.set(true);
  }
}
