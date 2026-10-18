import { HttpErrorResponse } from '@angular/common/http';
import { describeHttpError } from '../../core/api/describe-http-error';
import { ProblemDetails } from '../../core/api/problem-details';
import { translateServerMessage } from '../../core/i18n/server-messages';
import { Translate, englishT } from '../../core/i18n/translate-fn';

/** Server validation messages (400 ValidationProblemDetails) joined for display (known ones translated); falls back to the generic description. */
export function describeSaveError(error: HttpErrorResponse, fallback: string, t: Translate = englishT): string {
  const problem = error.error as ProblemDetails | null;
  const messages = problem?.errors ? Object.values(problem.errors).flat() : [];
  return messages.length > 0
    ? [...new Set(messages.map((message) => translateServerMessage(message, t)))].join(' ')
    : describeHttpError(error, fallback, t);
}
