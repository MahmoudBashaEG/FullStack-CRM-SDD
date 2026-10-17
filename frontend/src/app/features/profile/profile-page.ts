import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
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
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { describeHttpError } from '../../core/api/describe-http-error';
import { ProblemDetails } from '../../core/api/problem-details';
import { CurrentUser } from '../../core/auth/auth-models';
import { AuthService } from '../../core/auth/auth-service';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { MyCustomerProfile, ProfileApi } from './profile-api';

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  return group.get('newPassword')?.value === group.get('confirmPassword')?.value ? null : { mismatch: true };
}

@Component({
  selector: 'crm-profile-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    PageHeader,
    StatusBadge,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  templateUrl: './profile-page.html',
  styleUrl: './profile-page.scss',
})
export class ProfilePage {
  private readonly api = inject(ProfileApi);
  private readonly auth = inject(AuthService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly user = signal<CurrentUser | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly isCustomer = computed(() => this.auth.role() === 'Customer');
  protected readonly customerProfile = signal<MyCustomerProfile | null>(null);
  protected readonly customerProfileLoading = signal(false);
  protected readonly customerProfileError = signal<string | null>(null);

  protected readonly profileForm = this.fb.group({
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    phoneNumber: ['', [Validators.maxLength(32)]],
    company: ['', [Validators.maxLength(200)]],
  });
  protected readonly profileSaving = signal(false);
  protected readonly profileError = signal<string | null>(null);

  protected readonly passwordForm = this.fb.group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );
  protected readonly passwordSaving = signal(false);
  protected readonly passwordError = signal<string | null>(null);

  constructor() {
    this.api
      .getMe()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: user => {
          this.user.set(user);
          this.profileForm.reset({
            firstName: user.firstName,
            lastName: user.lastName,
            phoneNumber: user.phoneNumber ?? '',
          });
        },
        error: (error: HttpErrorResponse) =>
          this.loadError.set(describeHttpError(error, 'Could not load your profile.')),
      });

    if (this.isCustomer()) {
      this.loadCustomerProfile();
    }
  }

  /** Used by the constructor and the Retry button. */
  protected loadCustomerProfile(): void {
    this.customerProfileLoading.set(true);
    this.customerProfileError.set(null);
    this.api
      .getMyCustomerProfile()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: profile => {
          this.customerProfile.set(profile);
          this.profileForm.controls.company.setValue(profile.company ?? '');
          this.customerProfileLoading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.customerProfile.set(null);
          this.customerProfileError.set(describeHttpError(error, 'Could not load your customer details.'));
          this.customerProfileLoading.set(false);
        },
      });
  }

  protected saveProfile(): void {
    if (this.profileForm.invalid || this.profileSaving()) {
      this.profileForm.markAllAsTouched();
      return;
    }

    const value = this.profileForm.getRawValue();
    const firstName = value.firstName.trim();
    const lastName = value.lastName.trim();
    const phoneNumber = value.phoneNumber.trim() || null;
    this.profileSaving.set(true);
    this.profileError.set(null);

    const onError = (error: HttpErrorResponse) => {
      this.profileError.set(describeHttpError(error, 'Could not save your profile.'));
      this.profileSaving.set(false);
    };

    if (this.customerProfile() !== null) {
      // Customer with a loaded CRM profile: one call updates the account and the CRM row (plus Company).
      this.api
        .updateMyCustomerProfile({ firstName, lastName, phoneNumber, company: value.company.trim() || null })
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: p => {
            this.customerProfile.set(p);
            const current = this.user();
            if (current) {
              const updated = { ...current, firstName: p.firstName, lastName: p.lastName, phoneNumber: p.phoneNumber };
              this.user.set(updated);
              this.auth.updateCurrentUser(updated);
            }
            this.profileSaving.set(false);
            this.snackBar.open('Profile saved.', undefined, { duration: 3000 });
          },
          error: onError,
        });
      return;
    }

    this.api
      .updateMe({ firstName, lastName, phoneNumber })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: user => {
          this.user.set(user);
          this.auth.updateCurrentUser(user);
          this.profileSaving.set(false);
          this.snackBar.open('Profile saved.', undefined, { duration: 3000 });
        },
        error: onError,
      });
  }

  protected changePassword(): void {
    if (this.passwordForm.invalid || this.passwordSaving()) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    const { currentPassword, newPassword } = this.passwordForm.getRawValue();
    this.passwordSaving.set(true);
    this.passwordError.set(null);
    this.api
      .changePassword({ currentPassword, newPassword })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.snackBar.open('Password changed. Please sign in again.', undefined, { duration: 4000 });
          this.auth.endSession();
        },
        error: (error: HttpErrorResponse) => {
          const problem = error.error as ProblemDetails | null;
          this.passwordError.set(
            problem?.errors?.['currentPassword']?.[0] ?? describeHttpError(error, 'Could not change the password.'),
          );
          this.passwordSaving.set(false);
        },
      });
  }
}
