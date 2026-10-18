import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { AuthService } from '../../core/auth/auth-service';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { Agent } from '../agents/agent-models';
import { AgentsApi } from '../agents/agents-api';
import { AgentTask, LinkableTicket, fromLocalInput, toLocalInput } from './task-models';
import { TasksApi } from './tasks-api';
import { TicketPicker } from './ticket-picker';

type Card = 'details' | 'reminder' | 'status' | 'owner' | 'link';

@Component({
  selector: 'crm-task-edit-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DatePipe,
    PageHeader,
    StatusBadge,
    TicketPicker,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatCheckboxModule,
    MatButtonModule,
  ],
  templateUrl: './task-edit-page.html',
  styleUrl: '../../shared/ui/productivity-page.scss',
})
export class TaskEditPage {
  private readonly api = inject(TasksApi);
  private readonly auth = inject(AuthService);
  private readonly agentsApi = inject(AgentsApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly taskId = inject(ActivatedRoute).snapshot.paramMap.get('id');
  protected readonly isNew = this.taskId === null;
  protected readonly isSuperAdmin = computed(() => this.auth.role() === 'SuperAdmin');

  protected readonly task = signal<AgentTask | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly loading = signal(!this.isNew);
  /** One request at a time: a single tab never races itself on the task version. */
  protected readonly busy = signal(false);
  protected readonly errors = signal<Partial<Record<Card | 'create', string>>>({});
  protected readonly agents = signal<Agent[]>([]);
  protected readonly activeAgents = computed(() => this.agents().filter(a => a.isActive));

  // Create form and the details card share one form.
  protected readonly form = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', Validators.maxLength(2000)],
    due: [''],
    ownerId: [''],
    remind: [false],
    remindAt: [''],
  });
  protected readonly reminderForm = this.fb.group({ remindAt: ['', Validators.required] });
  protected readonly ownerForm = this.fb.group({ ownerId: ['', Validators.required], removeTicketLink: [false] });
  protected readonly selectedTicket = signal<LinkableTicket | null>(null);
  protected readonly changeTicket = signal<LinkableTicket | null>(null);
  protected readonly changingLink = signal(false);

  /** Owner used by the ticket picker: the selected owner (SuperAdmin), the current user (Agent) or the task's owner. */
  protected readonly pickerOwner = signal<string | null>(null);

  constructor() {
    if (this.auth.role() === 'SuperAdmin') {
      this.agentsApi.list().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: list => this.agents.set(list), error: () => this.agents.set([]) });
    }

    if (this.isNew) {
      this.pickerOwner.set(this.auth.role() === 'Agent' ? (this.auth.currentUser()?.id ?? null) : null);
      this.form.controls.ownerId.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(id => this.pickerOwner.set(id || null));
      this.form.controls.ownerId.addValidators(this.auth.role() === 'SuperAdmin' ? [Validators.required] : []);
    } else {
      this.reload(true);
    }
  }

  protected ownerName(): string {
    const t = this.task();
    if (t) return t.assignedAgentName;
    const id = this.form.controls.ownerId.value;
    const a = this.agents().find(x => x.id === id);
    return a ? `${a.firstName} ${a.lastName}` : 'the task owner';
  }

  protected create(): void {
    if (this.form.invalid || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    if (v.remind && !v.remindAt) {
      this.setError('create', 'Choose a reminder time or turn the reminder off.');
      return;
    }
    this.busy.set(true);
    this.setError('create', null);
    this.api
      .create({
        title: v.title.trim(),
        description: v.description.trim() || null,
        assignedAgentUserId: this.isSuperAdmin() ? v.ownerId : null,
        ticketId: this.selectedTicket()?.id ?? null,
        dueAtUtc: fromLocalInput(v.due),
        remindAtUtc: v.remind ? fromLocalInput(v.remindAt) : null,
      })
      .subscribe({
        next: created => {
          this.busy.set(false);
          this.snackBar.open('Task created.', 'OK', { duration: 4000 });
          void this.router.navigate(['/tasks', created.id]);
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.setError('create', describeHttpError(error, 'Could not create the task.'));
        },
      });
  }

  protected saveDetails(): void {
    const t = this.task();
    if (!t || this.form.controls.title.invalid || this.form.controls.description.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.run('details', this.api.update(t.id, t.version, {
      title: v.title.trim(),
      description: v.description.trim() || null,
      dueAtUtc: fromLocalInput(v.due),
    }), 'Task saved.');
  }

  protected saveReminder(): void {
    const t = this.task();
    const value = fromLocalInput(this.reminderForm.controls.remindAt.value);
    if (!t || !value) {
      this.reminderForm.markAllAsTouched();
      return;
    }
    this.run('reminder', this.api.scheduleReminder(t.id, t.version, value), 'Reminder scheduled.');
  }

  protected cancelReminder(): void {
    const t = this.task();
    if (t) this.run('reminder', this.api.cancelReminder(t.id, t.version), 'Reminder cancelled.');
  }

  protected setCompleted(completed: boolean): void {
    const t = this.task();
    if (t) this.run('status', completed ? this.api.complete(t.id, t.version) : this.api.reopen(t.id, t.version), completed ? 'Task completed.' : 'Task reopened.');
  }

  protected setArchived(archived: boolean): void {
    const t = this.task();
    if (t) this.run('status', archived ? this.api.archive(t.id, t.version) : this.api.restore(t.id, t.version), archived ? 'Task archived.' : 'Task restored.');
  }

  protected saveLink(): void {
    const t = this.task();
    const ticket = this.changeTicket();
    if (t && ticket) this.run('link', this.api.setTicketLink(t.id, t.version, ticket.id), 'Ticket linked.');
  }

  protected removeLink(): void {
    const t = this.task();
    if (t) this.run('link', this.api.removeTicketLink(t.id, t.version), 'Ticket link removed.');
  }

  protected reassign(): void {
    const t = this.task();
    if (!t || this.ownerForm.invalid) {
      this.ownerForm.markAllAsTouched();
      return;
    }
    const v = this.ownerForm.getRawValue();
    this.run('owner', this.api.reassign(t.id, t.version, v.ownerId, v.removeTicketLink), 'Task reassigned.');
  }

  /** F1: one request at a time, always with the displayed version; the response replaces the task (new version). */
  private run(card: Card, request: Observable<AgentTask>, success: string): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.setError(card, null);
    request.subscribe({
      next: updated => {
        this.busy.set(false);
        this.apply(updated, true);
        this.changingLink.set(false);
        this.snackBar.open(success, 'OK', { duration: 4000 });
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.onFailure(card, error);
      },
    });
  }

  private onFailure(card: Card, error: HttpErrorResponse): void {
    if (error.status === 404) {
      this.task.set(null);
      this.loadError.set('This task is no longer available to you.');
      return;
    }
    if (error.status === 409) {
      // Stale version or state conflict: show the message, reload the latest, keep dirty input; the user re-applies.
      this.setError(card, describeHttpError(error, 'The task was changed by someone else. Reload it and try again.'));
      this.reload(false);
      return;
    }
    if (error.status === 503) {
      this.setError(card, 'Your change may have been saved. Check it before trying again.');
      this.reload(false);
      return;
    }
    this.setError(card, describeHttpError(error, 'Could not save the change.')); // 400 inline, 428 generic
  }

  private reload(resetForms: boolean): void {
    if (!this.taskId) return;
    this.api.get(this.taskId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: latest => {
        this.loading.set(false);
        this.apply(latest, resetForms);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        if (error.status === 404) {
          this.task.set(null);
          this.loadError.set('This task is no longer available to you.');
        } else if (!this.task()) {
          this.loadError.set(describeHttpError(error, 'Could not load the task.'));
        }
      },
    });
  }

  /** Forms are reset only when not dirty, so typed input survives a conflict reload. */
  private apply(task: AgentTask, resetForms: boolean): void {
    this.task.set(task);
    this.pickerOwner.set(task.assignedAgentUserId);
    if (resetForms || !this.form.dirty) {
      this.form.reset({
        title: task.title, description: task.description ?? '', due: toLocalInput(task.dueAtUtc),
        ownerId: task.assignedAgentUserId, remind: false, remindAt: '',
      });
    }
    if (resetForms || !this.reminderForm.dirty) {
      this.reminderForm.reset({ remindAt: toLocalInput(task.remindAtUtc) });
    }
    if (resetForms || !this.ownerForm.dirty) {
      this.ownerForm.reset({ ownerId: task.assignedAgentUserId, removeTicketLink: false });
    }
  }

  private setError(card: Card | 'create', message: string | null): void {
    this.errors.update(current => ({ ...current, [card]: message ?? undefined }));
  }

  protected readonly controlOf = (name: string): FormControl => this.form.get(name) as FormControl;
}
