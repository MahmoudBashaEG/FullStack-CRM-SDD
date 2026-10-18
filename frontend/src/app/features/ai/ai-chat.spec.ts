import { chatRequestMessages, conversationFor } from './ai-chat';
import { AiChatTurn } from './ai-models';

describe('AI chat history', () => {
  it('sends the newest turns ending with the new message, never starting with an assistant turn, and resets when the ticket changes', () => {
    const turns: AiChatTurn[] = Array.from({ length: 6 }, (_, i) => ({
      role: i % 2 === 0 ? 'User' : 'Assistant',
      text: `message ${i}`,
      sources: i % 2 === 1 ? [{ articleId: 'a1', title: 'Reset a password' }] : [],
    }));

    const messages = chatRequestMessages(turns, '  And for a locked account?  ', 4);

    expect(messages).toEqual([
      { role: 'User', text: 'message 4' },
      { role: 'Assistant', text: 'message 5' },
      { role: 'User', text: 'And for a locked account?' },
    ]);

    const ticketChat = { ticketId: 't1', turns };
    expect(conversationFor(ticketChat, 't1')).toBe(ticketChat);
    expect(conversationFor(ticketChat, 't2')).toEqual({ ticketId: 't2', turns: [] });
    expect(conversationFor(ticketChat, null)).toEqual({ ticketId: null, turns: [] });
  });
});
