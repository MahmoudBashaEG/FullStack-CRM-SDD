import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { describeHttpError } from '../../core/api/describe-http-error';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { describeSaveError } from '../sla/sla-errors';
import { AdminApi } from './admin-api';
import { formatUtcTimestamp } from './admin-format';
import { OrganizationSettings } from './admin-models';
import { AdminSectionNav } from './admin-section-nav';

@Component({
  selector: 'crm-organization-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, PageHeader, AdminSectionNav, MatFormFieldModule, MatInputModule, MatButtonModule, MatProgressBarModule],
  templateUrl: './organization-settings-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './admin-page.scss'],
})
export class OrganizationSettingsPage {
  private readonly api = inject(AdminApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly formatUtcTimestamp = formatUtcTimestamp;
  protected readonly form = inject(NonNullableFormBuilder).group({
    displayName: ['', [Validators.required, Validators.maxLength(150)]],
    supportEmail: ['', [Validators.email, Validators.maxLength(256)]],
    supportPhone: ['', [Validators.maxLength(40)]],
    supportInformation: ['', [Validators.maxLength(1000)]],
  });
  protected readonly settings = signal<OrganizationSettings | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);
  protected readonly conflict = signal(false);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.api
      .getSettings()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: settings => {
          this.apply(settings);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.loadError.set(describeHttpError(error, 'Could not load the organization settings.'));
          this.loading.set(false);
        },
      });
  }

  protected reload(): void {
    this.saveError.set(null);
    this.conflict.set(false);
    this.load();
  }

  protected save(): void {
    const current = this.settings();
    if (!current || this.form.invalid || this.saving()) {
      return;
    }

    const v = this.form.getRawValue();
    this.saving.set(true);
    this.saveError.set(null);
    this.conflict.set(false);
    this.form.disable({ emitEvent: false });
    this.api
      .updateSettings(current.version, {
        displayName: v.displayName,
        supportEmail: v.supportEmail || null,
        supportPhone: v.supportPhone || null,
        supportInformation: v.supportInformation || null,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: saved => {
          this.snackBar.open(
            saved.version === current.version ? 'No changes to save.' : 'Organization settings saved.',
            'Close',
            { duration: 4000 },
          );
          this.apply(saved);
          this.saving.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.form.enable({ emitEvent: false });
          this.saving.set(false);
          if (error.status === 409) {
            this.conflict.set(true);
            this.saveError.set('These settings were changed by someone else. Reload to see the latest values.');
          } else if (error.status === 400) {
            this.saveError.set(describeSaveError(error, 'Could not save the settings.'));
          } else {
            this.saveError.set(describeHttpError(error, 'Could not save the settings.'));
          }
        },
      });
  }

  private apply(settings: OrganizationSettings): void {
    this.settings.set(settings);
    this.form.enable({ emitEvent: false });
    this.form.reset({
      displayName: settings.displayName ?? '',
      supportEmail: settings.supportEmail ?? '',
      supportPhone: settings.supportPhone ?? '',
      supportInformation: settings.supportInformation ?? '',
    });
  }
}
