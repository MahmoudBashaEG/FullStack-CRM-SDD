import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { AuthService } from '../../core/auth/auth-service';
import { AiConversation, EMPTY_CONVERSATION, conversationFor } from './ai-chat';
import { AiChatTurn } from './ai-models';

/**
 * AI-001 chat history: browser memory only (no localStorage/sessionStorage). Cleared by New conversation, a ticket switch,
 * access loss, logout or a different user signing in. A page reload starts empty.
 */
@Injectable({ providedIn: 'root' })
export class AiChatStore {
  private readonly conversation = signal<AiConversation>(EMPTY_CONVERSATION);

  readonly ticketId = computed(() => this.conversation().ticketId);
  readonly turns = computed(() => this.conversation().turns);

  constructor() {
    const auth = inject(AuthService);
    let owner = auth.currentUser()?.id ?? null;
    effect(() => {
      const id = auth.currentUser()?.id ?? null;
      if (id !== owner) {
        owner = id;
        untracked(() => this.reset());
      }
    });
  }

  open(ticketId: string | null): void {
    this.conversation.update(c => conversationFor(c, ticketId));
  }

  append(user: AiChatTurn, assistant: AiChatTurn): void {
    this.conversation.update(c => ({ ticketId: c.ticketId, turns: [...c.turns, user, assistant] }));
  }

  /** New conversation: keeps the selected ticket, drops the turns. */
  clear(): void {
    this.conversation.update(c => ({ ticketId: c.ticketId, turns: [] }));
  }

  reset(): void {
    this.conversation.set(EMPTY_CONVERSATION);
  }
}
