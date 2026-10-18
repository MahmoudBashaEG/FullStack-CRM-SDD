import { SERVER_MESSAGE_PATTERNS } from './server-messages.patterns';
import { Translate, lookup } from './translate-fn';
import { EN } from './en';

const STATUS_PARAMS = new Set(['status', 'from', 'to']);

/**
 * PLAT-001 D13: translates a KNOWN backend message (exact text or a known template); any other server text is returned verbatim
 * (never dropped). The backend messages themselves are unchanged. Status names inside a sentence use the ticket status labels.
 */
export function translateServerMessage(message: string, t: Translate): string {
  for (const [key, pattern, names] of SERVER_MESSAGE_PATTERNS) {
    const match = pattern.exec(message);
    if (!match) {
      continue;
    }
    const params: Record<string, string> = {};
    names.forEach((name, index) => {
      const value = match[index + 1];
      params[name] = STATUS_PARAMS.has(name) && lookup(EN, `enums.ticketStatus.${value}`) !== null ? t(`enums.ticketStatus.${value}`) : value;
    });
    return t(`serverMessages.${key}`, params);
  }
  return message;
}
