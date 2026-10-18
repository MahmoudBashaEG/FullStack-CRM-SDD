import { Clipboard } from '@angular/cdk/clipboard';
import { MatSnackBar } from '@angular/material/snack-bar';

/** Copies exactly the stored body (no trimming, no substitution). Nothing is ever sent anywhere. */
export function copyQuickReply(clipboard: Clipboard, snackBar: MatSnackBar, body: string): void {
  const ok = clipboard.copy(body);
  snackBar.open(ok ? 'Copied to clipboard.' : 'Could not copy. Select the text and copy it manually.', 'OK', { duration: 3000 });
}
