import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/describe-http-error';
import { I18n } from '../../core/i18n/i18n';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { Agent } from './agent-models';
import { AgentsApi } from './agents-api';

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  return group.get('newPassword')?.value === group.get('confirmPassword')?.value ? null : { mismatch: true };
}

@Component({
  selector: 'crm-agent-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    ErrorState,
    TranslatePipe,
    StatusBadge,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatSlideToggleModule,
  ],
  templateUrl: './agent-detail-page.html',
  styleUrl: './agent-detail-page.scss',
})
export class AgentDetailPage {
  private readonly i18n = inject(I18n);
  private readonly api = inject(AgentsApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';

  protected readonly agent = signal<Agent | null>(null);
  protected readonly loadError = signal<string | null>(null);

  protected readonly detailsForm = this.fb.group({
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    phoneNumber: ['', [Validators.maxLength(32)]],
  });
  protected readonly detailsSaving = signal(false);
  protected readonly detailsError = signal<string | null>(null);

  protected readonly statusSaving = signal(false);
  protected readonly statusError = signal<string | null>(null);

  protected readonly passwordForm = this.fb.group(
    {
      newPassword: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );
  protected readonly passwordSaving = signal(false);
  protected readonly passwordError = signal<string | null>(null);

  constructor() {
    this.load();
  }

  protected reload(): void {
    this.loadError.set(null);
    this.load();
  }

  private load(): void {
    this.api
      .get(this.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: agent => this.show(agent),
        error: (error: HttpErrorResponse) =>
          this.loadError.set(describeHttpError(error, this.i18n.t('agents.detail.loadFailed'), this.i18n.t)),
      });
  }

  private show(agent: Agent): void {
    this.agent.set(agent);
    this.detailsForm.reset({
      firstName: agent.firstName,
      lastName: agent.lastName,
      phoneNumber: agent.phoneNumber ?? '',
    });
  }

  protected saveDetails(): void {
    if (this.detailsForm.invalid || this.detailsSaving()) {
      this.detailsForm.markAllAsTouched();
      return;
    }

    const value = this.detailsForm.getRawValue();
    this.detailsSaving.set(true);
    this.detailsError.set(null);
    this.api
      .update(this.id, {
        firstName: value.firstName.trim(),
        lastName: value.lastName.trim(),
        phoneNumber: value.phoneNumber.trim() || null,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: agent => {
          this.show(agent);
          this.detailsSaving.set(false);
          this.snackBar.open(this.i18n.t('agents.detail.updated'), undefined, { duration: 3000 });
        },
        error: (error: HttpErrorResponse) => {
          this.detailsError.set(describeHttpError(error, this.i18n.t('agents.detail.updateFailed'), this.i18n.t));
          this.detailsSaving.set(false);
        },
      });
  }

  protected toggleActive(isActive: boolean): void {
    const current = this.agent();
    if (!current || this.statusSaving()) {
      return;
    }

    this.statusSaving.set(true);
    this.statusError.set(null);
    this.api
      .setStatus(this.id, isActive)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.agent.set({ ...current, isActive });
          this.statusSaving.set(false);
          this.snackBar.open(
            isActive ? this.i18n.t('agents.detail.enabled') : this.i18n.t('agents.detail.disabled'),
            undefined,
            { duration: 4000 },
          );
        },
        error: (error: HttpErrorResponse) => {
          // Re-assign so the toggle is bound back to the server's value.
          this.agent.set({ ...current });
          this.statusError.set(describeHttpError(error, this.i18n.t('agents.detail.statusFailed'), this.i18n.t));
          this.statusSaving.set(false);
        },
      });
  }

  protected setPassword(): void {
    if (this.passwordForm.invalid || this.passwordSaving()) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    this.passwordSaving.set(true);
    this.passwordError.set(null);
    this.api
      .setPassword(this.id, this.passwordForm.getRawValue().newPassword)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.passwordForm.reset();
          this.passwordSaving.set(false);
          this.snackBar.open(this.i18n.t('agents.detail.passwordUpdated'), undefined, {
            duration: 4000,
          });
        },
        error: (error: HttpErrorResponse) => {
          this.passwordError.set(describeHttpError(error, this.i18n.t('agents.detail.passwordFailed'), this.i18n.t));
          this.passwordSaving.set(false);
        },
      });
  }
}
