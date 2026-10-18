import { HttpErrorResponse } from '@angular/common/http';
import { translateServerMessage } from '../i18n/server-messages';
import { Translate, englishT } from '../i18n/translate-fn';
import { ProblemDetails } from './problem-details';

const STATUS_KEYS: Readonly<Record<number, string>> = {
  401: 'errors.unauthorized',
  403: 'errors.forbidden',
  404: 'errors.notFound',
  409: 'errors.conflict',
  413: 'errors.tooLarge',
  428: 'errors.preconditionRequired',
};

/**
 * User-facing message for a failed API call (PLAT-001 D13): the server's ProblemDetails detail (translated when it is a known message,
 * otherwise shown verbatim), else a status-based message, else the caller's (already translated) fallback.
 * `t` defaults to English so callers and specs that do not pass a translator behave exactly as before.
 */
export function describeHttpError(error: HttpErrorResponse, fallback: string, t: Translate = englishT): string {
  if (error.status === 0) {
    return t('errors.unreachable');
  }
  if (error.status === 429) {
    return t('errors.tooMany');
  }
  const problem = error.error as ProblemDetails | null;
  if (problem?.detail) {
    return translateServerMessage(problem.detail, t);
  }
  const statusKey = STATUS_KEYS[error.status] ?? (error.status >= 500 ? 'errors.server' : null);
  if (statusKey) {
    return t(statusKey);
  }
  return problem?.title ? translateServerMessage(problem.title, t) : fallback;
}
