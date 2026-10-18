import { ChangeDetectionStrategy, Component, Injectable, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { Observable, map } from 'rxjs';
import { I18n } from '../../../core/i18n/i18n';

export interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  /** Destructive actions use the error color on the confirm button. */
  destructive?: boolean;
}

/** Translated, focus-trapping replacement for window.confirm. Direction follows the root Directionality. */
@Component({
  selector: 'crm-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>{{ data.message }}</mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" [mat-dialog-close]="false">{{ data.cancelLabel ?? i18n.t('common.cancel') }}</button>
      <button mat-flat-button type="button" [class.destructive]="data.destructive" [mat-dialog-close]="true" cdkFocusInitial>
        {{ data.confirmLabel ?? i18n.t('common.confirm') }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    mat-dialog-content {
      overflow-wrap: anywhere;
    }
    .destructive {
      --mat-button-filled-container-color: var(--mat-sys-error);
      --mat-button-filled-label-text-color: var(--mat-sys-on-error);
    }
  `,
})
export class ConfirmDialog {
  protected readonly data = inject<ConfirmOptions>(MAT_DIALOG_DATA);
  protected readonly i18n = inject(I18n);
}

@Injectable({ providedIn: 'root' })
export class Confirm {
  private readonly dialog = inject(MatDialog);

  /** Emits true once when the user confirms, false when they cancel or dismiss. */
  ask(options: ConfirmOptions): Observable<boolean> {
    return this.dialog
      .open<ConfirmDialog, ConfirmOptions, boolean>(ConfirmDialog, { data: options, maxWidth: '95vw', width: '420px', autoFocus: 'first-tabbable' })
      .afterClosed()
      .pipe(map((result) => result === true));
  }
}
