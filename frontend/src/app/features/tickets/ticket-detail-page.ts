import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, WritableSignal, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { AuthService } from '../../core/auth/auth-service';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { Agent } from '../agents/agent-models';
import { AgentsApi } from '../agents/agents-api';
import { TicketCollaborators } from '../collaboration/ticket-collaborators';
import { TicketDiscussion } from '../collaboration/ticket-discussion';
import { TicketKnowledgeArticles } from '../knowledge-base/ticket-knowledge-articles';
import { QuickReplyPanel } from '../quick-replies/quick-reply-panel';
import { TicketActivityList } from './ticket-activity-list';
import { TicketSlaCard } from './ticket-sla-card';
import { canMarkFirstResponse } from './ticket-sla';
import {
  TICKET_CATEGORIES,
  TICKET_PRIORITIES,
  Ticket,
  TicketCategory,
  TicketPriority,
  TicketStatus,
  priorityTone,
  statusLabel,
  statusTone,
} from './ticket-models';
import { ticketActions } from './ticket-workflow';
import { TicketsApi } from './tickets-api';

@Component({
  selector: 'crm-ticket-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DatePipe,
    PageHeader,
    StatusBadge,
    TicketActivityList,
    TicketSlaCard,
    TicketDiscussion,
    TicketCollaborators,
    QuickReplyPanel,
    TicketKnowledgeArticles,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
  ],
  templateUrl: './ticket-detail-page.html',
  styleUrl: './ticket-detail-page.scss',
})
export class TicketDetailPage {
  private readonly api = inject(TicketsApi);
  private readonly auth = inject(AuthService);
  private readonly agentsApi = inject(AgentsApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';

  protected readonly categories = TICKET_CATEGORIES;
  protected readonly priorities = TICKET_PRIORITIES;
  protected readonly priorityTone = priorityTone;
  protected readonly statusLabel = statusLabel;
  protected readonly statusTone = statusTone;
  protected readonly ticket = signal<Ticket | null>(null);
  protected readonly loadError = signal<string | null>(null);

  protected readonly isSuperAdmin = computed(() => this.auth.role() === 'SuperAdmin');
  protected readonly actions = computed(() => {
    const t = this.ticket();
    return t ? ticketActions(t, this.auth.currentUser()) : null;
  });
  protected readonly canMarkResponded = computed(() => {
    const t = this.ticket();
    return t ? canMarkFirstResponse(t, this.auth.currentUser()) : false;
  });
  protected readonly activeAgents = signal<Agent[]>([]);
  protected readonly agentsError = signal<string | null>(null);
  protected readonly selectedAssignee = signal<string | null>(null);
  protected readonly escalationReason = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(500)],
  });
  protected readonly busy = signal(false); // one action at a time
  protected readonly actionError = signal<string | null>(null);
  protected readonly activityRefresh = signal(0);

  protected readonly form = this.fb.group({
    subject: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(4000)]],
    category: this.fb.control<TicketCategory>('General', Validators.required),
    priority: this.fb.control<TicketPriority>('Medium', Validators.required),
  });
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  constructor() {
    this.api
      .get(this.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ticket => this.show(ticket),
        error: (error: HttpErrorResponse) =>
          this.loadError.set(describeHttpError(error, 'Could not load the ticket.')),
      });

    // Agents never call /api/v1/agents (SuperAdmin-only endpoint); the server re-validates the assignee anyway.
    if (this.isSuperAdmin()) {
      this.agentsApi
        .list()
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: agents => this.activeAgents.set(agents.filter(a => a.isActive)),
          error: (error: HttpErrorResponse) =>
            this.agentsError.set(describeHttpError(error, 'Could not load agents.')),
        });
    }
  }

  /** resetInputs = false (refresh after a 409): a conflict never discards text the user typed. */
  private show(ticket: Ticket, resetInputs = true): void {
    this.ticket.set(ticket);
    if (resetInputs || !this.form.dirty) {
      this.form.reset({
        subject: ticket.subject,
        description: ticket.description,
        category: ticket.category,
        priority: ticket.priority,
      });
    }
    this.selectedAssignee.set(ticket.assignedAgentUserId);
    if (resetInputs) {
      this.escalationReason.reset('');
    }
  }

  protected save(): void {
    if (this.form.invalid || this.saving() || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    this.saveError.set(null);
    this.actionError.set(null);
    this.api
      .update(this.id, {
        subject: value.subject.trim(),
        description: value.description.trim(),
        category: value.category,
        priority: value.priority,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: updated => {
          this.show(updated);
          this.activityRefresh.update(v => v + 1);
          this.saving.set(false);
          this.snackBar.open('Ticket updated.', undefined, { duration: 3000 });
        },
        error: (error: HttpErrorResponse) => {
          this.saving.set(false);
          this.handleActionError(error, this.saveError);
        },
      });
  }

  protected transition(current: Ticket, to: TicketStatus): void {
    this.run(this.api.transition(this.id, { fromStatus: current.status, toStatus: to }), `Status changed to ${statusLabel(to)}.`);
  }

  /** True while the agent list has not loaded (or failed), so the "inactive" hint is never shown on a guess. */
  protected isAgentActive(agentId: string): boolean {
    return this.activeAgents().length === 0 || this.agentsError() !== null || this.activeAgents().some(a => a.id === agentId);
  }

  protected saveAssignee(): void {
    this.run(this.api.changeAssignee(this.id, { assignedAgentUserId: this.selectedAssignee() }), 'Assignment updated.');
  }

  protected markResponded(): void {
    this.run(this.api.recordFirstResponse(this.id), 'First response recorded.');
  }

  protected escalate(): void {
    if (this.escalationReason.invalid) {
      this.escalationReason.markAsTouched();
      return;
    }
    this.run(this.api.escalate(this.id, { reason: this.escalationReason.value.trim() }), 'Ticket escalated.');
  }

  protected clearEscalation(): void {
    this.run(this.api.clearEscalation(this.id), 'Escalation cleared.');
  }

  private run(request: Observable<Ticket>, success: string): void {
    if (this.busy() || this.saving()) {
      return;
    }
    this.busy.set(true);
    this.actionError.set(null);
    this.saveError.set(null);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ticket => {
        this.show(ticket);
        this.activityRefresh.update(v => v + 1);
        this.busy.set(false);
        this.snackBar.open(success, undefined, { duration: 3000 });
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.handleActionError(error);
      },
    });
  }

  /**
   * 404 after load = the ticket left the caller's scope (e.g. reassigned) ⇒ drop stale content.
   * 409 = stale or overlapping change; nothing was saved ⇒ show the server message, refresh ticket + activity
   * so the buttons match the real state, keep the user's typed input, let them retry.
   */
  private handleActionError(error: HttpErrorResponse, target: WritableSignal<string | null> = this.actionError): void {
    if (error.status === 404) {
      this.ticket.set(null);
      this.loadError.set('This ticket is no longer available to you. It may have been reassigned.');
      return;
    }
    target.set(describeHttpError(error, 'The action could not be completed.'));
    if (error.status === 409) {
      this.reload();
    }
  }

  /** The internal discussion returned 404: the caller lost access, same handling as any other 404 on this page. */
  protected onAccessLost(): void {
    this.ticket.set(null);
    this.loadError.set('This ticket is no longer available to you. It may have been reassigned.');
  }

  private reload(): void {
    this.api
      .get(this.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ticket => {
          this.show(ticket, false); // keep unsaved edits / escalation reason
          this.activityRefresh.update(v => v + 1);
        },
        error: (error: HttpErrorResponse) => this.handleActionError(error),
      });
  }
}
