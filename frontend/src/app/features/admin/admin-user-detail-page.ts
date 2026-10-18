import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/describe-http-error';
import { AuthService } from '../../core/auth/auth-service';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { AdminApi } from './admin-api';
import { canDeactivate, formatUtcTimestamp, statusConfirmation } from './admin-format';
import { AdminAccount } from './admin-models';
import { AdminSectionNav } from './admin-section-nav';

@Component({
  selector: 'crm-admin-user-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, PageHeader, StatusBadge, AdminSectionNav, MatButtonModule, MatProgressBarModule],
  templateUrl: './admin-user-detail-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './admin-page.scss'],
})
export class AdminUserDetailPage {
  private readonly api = inject(AdminApi);
  private readonly auth = inject(AuthService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';

  protected readonly account = signal<AdminAccount | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly formatUtcTimestamp = formatUtcTimestamp;

  constructor() {
    this.load();
  }

  protected isSelf(account: AdminAccount): boolean {
    return account.id === this.auth.currentUser()?.id;
  }

  protected canDeactivate(account: AdminAccount): boolean {
    return canDeactivate(account, this.auth.currentUser()?.id ?? null);
  }

  protected load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.api
      .getUser(this.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: account => {
          this.account.set(account);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.loadError.set(describeHttpError(error, 'Could not load the account.'));
          this.loading.set(false);
        },
      });
  }

  protected changeStatus(account: AdminAccount, activate: boolean): void {
    if (!window.confirm(statusConfirmation(account, activate))) {
      return;
    }

    this.saving.set(true);
    this.actionError.set(null);
    this.api
      .setUserStatus(account.id, activate)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: response => {
          this.account.set(response.account);
          this.saving.set(false);
          this.snackBar.open(
            !response.changed
              ? `No change: the account was already ${response.account.isActive ? 'active' : 'inactive'}.`
              : activate
                ? 'Account reactivated.'
                : 'Account deactivated. All sessions were signed out.',
            'Close',
            { duration: 5000 },
          );
        },
        error: (error: HttpErrorResponse) => {
          this.saving.set(false);
          this.actionError.set(describeHttpError(error, 'Could not change the account status.'));
        },
      });
  }
}
