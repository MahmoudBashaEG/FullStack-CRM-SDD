import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormArray, FormGroup, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { describeHttpError } from '../../core/api/describe-http-error';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { TICKET_PRIORITIES, TicketPriority } from '../tickets/ticket-models';
import { SlaApi } from './sla-api';
import { describeSaveError } from './sla-errors';
import { SlaPolicy, formatBusinessMinutes } from './sla-models';

const DEMO_DEFAULTS: Record<TicketPriority, [number, number]> = {
  Urgent: [120, 240],
  High: [240, 960],
  Medium: [480, 1440],
  Low: [960, 2880],
};

/** SLA-001 capability 1: one global policy; four first-response / resolution targets in business minutes. */
@Component({
  selector: 'crm-sla-policy-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    LocalizedDatePipe,
    TranslatePipe,
    ErrorState,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSlideToggleModule,
  ],
  templateUrl: './sla-policy-form.html',
  styleUrl: './sla-admin.scss',
})
export class SlaPolicyForm {
  protected readonly i18n = inject(I18n);
  private readonly api = inject(SlaApi);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly format = formatBusinessMinutes;
  protected readonly policy = signal<SlaPolicy | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  protected readonly form = this.fb.group({
    isEnabled: [true],
    rows: this.fb.array(
      TICKET_PRIORITIES.map(priority =>
        this.fb.group({
          priority: [priority as TicketPriority],
          first: [DEMO_DEFAULTS[priority][0], [Validators.required, Validators.min(1), Validators.max(525_600)]],
          resolution: [DEMO_DEFAULTS[priority][1], [Validators.required, Validators.min(1), Validators.max(525_600)]],
        }),
      ),
    ),
  });

  constructor() {
    this.load();
  }

  protected get rows(): FormArray<FormGroup> {
    return this.form.controls.rows as unknown as FormArray<FormGroup>;
  }

  protected load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.api
      .getPolicy()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: policy => {
          this.show(policy);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.loadError.set(describeHttpError(error, this.i18n.t('sla.policy.loadFailed'), this.i18n.t));
          this.loading.set(false);
        },
      });
  }

  protected save(): void {
    this.rows.controls.forEach(group => group.markAllAsTouched());
    if (this.form.invalid || this.saving()) {
      return;
    }
    const value = this.form.getRawValue();
    this.saving.set(true);
    this.saveError.set(null);
    this.api
      .savePolicy({
        isEnabled: value.isEnabled,
        targets: value.rows.map(r => ({
          priority: r.priority,
          firstResponseMinutes: Number(r.first),
          resolutionMinutes: Number(r.resolution),
        })),
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: policy => {
          this.show(policy);
          this.saving.set(false);
          this.snackBar.open(this.i18n.t('sla.policy.saved'), undefined, { duration: 3000 });
        },
        error: (error: HttpErrorResponse) => {
          this.saving.set(false);
          this.saveError.set(describeSaveError(error, this.i18n.t('sla.policy.saveFailed'), this.i18n.t));
        },
      });
  }

  private show(policy: SlaPolicy): void {
    this.policy.set(policy);
    if (!policy.isConfigured) {
      return; // keep the suggested demo values; nothing is saved until the SuperAdmin presses Save
    }
    this.form.controls.isEnabled.setValue(policy.isEnabled);
    for (const group of this.rows.controls) {
      const target = policy.targets.find(t => t.priority === group.value['priority']);
      if (target) {
        group.patchValue({ first: target.firstResponseMinutes, resolution: target.resolutionMinutes });
      }
    }
    this.form.markAsPristine();
  }
}
