import { Clipboard } from '@angular/cdk/clipboard';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Translate, englishT } from '../../core/i18n/translate-fn';

/** Copies exactly the stored body (no trimming, no substitution). Nothing is ever sent anywhere. */
export function copyQuickReply(clipboard: Clipboard, snackBar: MatSnackBar, body: string, t: Translate = englishT): void {
  const ok = clipboard.copy(body);
  snackBar.open(t(ok ? 'quickReplies.copied' : 'quickReplies.copyFailed'), t('quickReplies.ok'), { duration: 3000 });
}
