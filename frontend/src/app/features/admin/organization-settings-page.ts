import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatRadioModule } from '@angular/material/radio';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { Branding } from '../../core/branding/branding';
import { ACCENT_PRESETS, AccentPreset } from '../../core/branding/branding-api';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { Confirm } from '../../shared/ui/confirm-dialog/confirm-dialog';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { describeSaveError } from '../sla/sla-errors';
import { AdminApi } from './admin-api';
import { OrganizationSettings } from './admin-models';
import { AdminSectionNav } from './admin-section-nav';

const LOGO_TYPES = ['image/png', 'image/jpeg', 'image/webp'];
const LOGO_MAX_BYTES = 512 * 1024;

@Component({
  selector: 'crm-organization-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    PageHeader,
    AdminSectionNav,
    ErrorState,
    TranslatePipe,
    LocalizedDatePipe,
    MatCardModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatButtonModule,
    MatProgressBarModule,
    MatRadioModule,
  ],
  templateUrl: './organization-settings-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './admin-page.scss', './organization-settings-page.scss'],
})
export class OrganizationSettingsPage {
  private readonly api = inject(AdminApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly confirm = inject(Confirm);
  protected readonly i18n = inject(I18n);
  protected readonly branding = inject(Branding);

  protected readonly presets = ACCENT_PRESETS;
  protected readonly form = inject(NonNullableFormBuilder).group({
    displayName: ['', [Validators.required, Validators.maxLength(150)]],
    supportEmail: ['', [Validators.email, Validators.maxLength(256)]],
    supportPhone: ['', [Validators.maxLength(40)]],
    supportInformation: ['', [Validators.maxLength(1000)]],
    accentColor: ['violet' as AccentPreset],
  });
  protected readonly settings = signal<OrganizationSettings | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);
  protected readonly conflict = signal(false);
  protected readonly selectedFile = signal<File | null>(null);
  protected readonly fileError = signal<string | null>(null);

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
          this.loadError.set(describeHttpError(error, this.i18n.t('admin.settings.loadError'), this.i18n.t));
          this.loading.set(false);
        },
      });
  }

  protected reload(): void {
    this.saveError.set(null);
    this.conflict.set(false);
    this.selectedFile.set(null);
    this.fileError.set(null);
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
        accentColor: v.accentColor,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: saved => {
          this.snackBar.open(
            this.i18n.t(saved.version === current.version ? 'admin.settings.noChanges' : 'admin.settings.saved'),
            this.i18n.t('common.close'),
            { duration: 4000 },
          );
          this.apply(saved);
          this.saving.set(false);
          void this.branding.reload();
        },
        error: (error: HttpErrorResponse) => {
          this.form.enable({ emitEvent: false });
          this.saving.set(false);
          this.handleError(error, 'admin.settings.saveError');
        },
      });
  }

  protected onFileChosen(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    input.value = '';
    this.selectedFile.set(null);
    this.fileError.set(null);
    if (!file) {
      return;
    }
    if (!LOGO_TYPES.includes(file.type)) {
      this.fileError.set(this.i18n.t('branding.logoBadType'));
    } else if (file.size > LOGO_MAX_BYTES) {
      this.fileError.set(this.i18n.t('branding.logoTooLarge'));
    } else {
      this.selectedFile.set(file);
    }
  }

  protected uploadLogo(): void {
    const current = this.settings();
    const file = this.selectedFile();
    if (!current || !file || this.saving()) {
      return;
    }
    this.runLogoChange(this.api.setLogo(current.version, file), 'branding.logoUploaded', 'admin.settings.logoUploadError');
  }

  protected removeLogo(): void {
    const current = this.settings();
    if (!current || this.saving()) {
      return;
    }
    this.confirm
      .ask({
        title: this.i18n.t('branding.removeLogoTitle'),
        message: this.i18n.t('branding.removeLogoMessage'),
        confirmLabel: this.i18n.t('branding.removeLogo'),
        destructive: true,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(confirmed => {
        if (confirmed) {
          this.runLogoChange(this.api.removeLogo(current.version), 'branding.logoRemoved', 'admin.settings.logoRemoveError');
        }
      });
  }

  private runLogoChange(request: Observable<OrganizationSettings>, successKey: string, fallbackKey: string): void {
    this.saving.set(true);
    this.saveError.set(null);
    this.conflict.set(false);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: saved => {
        this.selectedFile.set(null);
        this.fileError.set(null);
        this.apply(saved, true);
        this.saving.set(false);
        void this.branding.reload();
        this.snackBar.open(this.i18n.t(successKey), this.i18n.t('common.close'), { duration: 4000 });
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.handleError(error, fallbackKey);
      },
    });
  }

  private handleError(error: HttpErrorResponse, fallbackKey: string): void {
    const fallback = this.i18n.t(fallbackKey);
    if (error.status === 409) {
      this.conflict.set(true);
      this.saveError.set(this.i18n.t('admin.settings.conflict'));
    } else if (error.status === 400) {
      this.saveError.set(describeSaveError(error, fallback, this.i18n.t));
    } else {
      this.saveError.set(describeHttpError(error, fallback, this.i18n.t));
    }
  }

  /** Unsaved text edits survive a logo change: when the form is dirty only the settings (and so the version) are replaced. */
  private apply(settings: OrganizationSettings, keepEdits = false): void {
    this.settings.set(settings);
    if (keepEdits && this.form.dirty) {
      return;
    }
    this.form.enable({ emitEvent: false });
    this.form.reset({
      displayName: settings.displayName ?? '',
      supportEmail: settings.supportEmail ?? '',
      supportPhone: settings.supportPhone ?? '',
      supportInformation: settings.supportInformation ?? '',
      accentColor: settings.accentColor ?? 'violet',
    });
  }
}
