import { AI_MAX_CHAT_MESSAGES, AiChatMessage, AiChatTurn } from './ai-models';

export interface AiConversation {
  readonly ticketId: string | null;
  readonly turns: readonly AiChatTurn[];
}

export const EMPTY_CONVERSATION: AiConversation = { ticketId: null, turns: [] };

/** Same ticket (or both general) ⇒ keep; anything else ⇒ a fresh conversation, so facts never carry over to another ticket. */
export function conversationFor(current: AiConversation, ticketId: string | null): AiConversation {
  return current.ticketId === ticketId ? current : { ticketId, turns: [] };
}

/** Newest messages within the cap, ending with the new user message, never starting with an Assistant turn. Sources are not sent. */
export function chatRequestMessages(turns: readonly AiChatTurn[], text: string, max = AI_MAX_CHAT_MESSAGES): AiChatMessage[] {
  const all: AiChatMessage[] = [...turns.map(t => ({ role: t.role, text: t.text })), { role: 'User', text: text.trim() }];
  let recent = all.slice(-max);
  while (recent.length > 1 && recent[0].role === 'Assistant') {
    recent = recent.slice(1);
  }
  return recent;
}
