import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/describe-http-error';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { AgentsApi } from './agents-api';

@Component({
  selector: 'crm-agent-create-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  templateUrl: './agent-create-page.html',
  styleUrl: './agent-create-page.scss',
})
export class AgentCreatePage {
  private readonly api = inject(AgentsApi);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly form = inject(NonNullableFormBuilder).group({
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    phoneNumber: ['', [Validators.maxLength(32)]],
    password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]],
  });
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.submitting.set(true);
    this.error.set(null);
    this.api
      .create({
        firstName: value.firstName.trim(),
        lastName: value.lastName.trim(),
        email: value.email.trim(),
        phoneNumber: value.phoneNumber.trim() || null,
        password: value.password,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: agent => {
          this.snackBar.open('Agent created.', undefined, { duration: 3000 });
          void this.router.navigate(['/agents', agent.id]);
        },
        error: (error: HttpErrorResponse) => {
          this.error.set(describeHttpError(error, 'Could not create the agent.'));
          this.submitting.set(false);
        },
      });
  }
}
