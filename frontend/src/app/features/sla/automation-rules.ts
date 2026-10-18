import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { forkJoin, of, startWith } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { describeHttpError } from '../../core/api/describe-http-error';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { Agent } from '../agents/agent-models';
import { AgentsApi } from '../agents/agents-api';
import { TICKET_CATEGORIES, TICKET_PRIORITIES, TicketCategory, TicketPriority } from '../tickets/ticket-models';
import { SlaApi } from './sla-api';
import { describeSaveError } from './sla-errors';
import {
  ACTIONS_BY_TRIGGER,
  AUTOMATION_TRIGGERS,
  AutomationAction,
  AutomationRule,
  AutomationRuleRequest,
  AutomationTrigger,
  SlaKind,
} from './sla-models';

const TRIGGER_LABELS: Record<AutomationTrigger, string> = {
  TicketCreated: 'Ticket created',
  SlaWarningDue: 'SLA warning due',
  SlaBreached: 'SLA breached',
};

const ACTION_LABELS: Record<AutomationAction, string> = {
  AssignToAgent: 'Assign to agent',
  CreateInAppNotification: 'Create in-app notification',
  EscalateTicket: 'Escalate ticket',
};

/** SLA-001 capabilities 3, 5, 6: SuperAdmin-managed rules. Only the server-valid trigger/action combinations are offered. */
@Component({
  selector: 'crm-automation-rules',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    StatusBadge,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
  ],
  templateUrl: './automation-rules.html',
  styleUrl: './sla-admin.scss',
})
export class AutomationRules {
  private readonly api = inject(SlaApi);
  private readonly agentsApi = inject(AgentsApi);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly triggers = AUTOMATION_TRIGGERS;
  protected readonly categories: readonly TicketCategory[] = TICKET_CATEGORIES;
  protected readonly priorities: readonly TicketPriority[] = TICKET_PRIORITIES;
  protected readonly triggerLabel = (t: AutomationTrigger) => TRIGGER_LABELS[t];
  protected readonly actionLabel = (a: AutomationAction) => ACTION_LABELS[a];

  protected readonly rules = signal<AutomationRule[]>([]);
  protected readonly agents = signal<Agent[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);
  /** null = form closed; '' = new rule; otherwise the id being edited. */
  protected readonly editing = signal<string | null>(null);

  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(100)]],
    isEnabled: [true],
    trigger: ['TicketCreated' as AutomationTrigger],
    action: ['AssignToAgent' as AutomationAction],
    sortOrder: [10, [Validators.required, Validators.min(1), Validators.max(1000)]],
    categoryCondition: [null as TicketCategory | null],
    priorityCondition: [null as TicketPriority | null],
    assignedCondition: [null as boolean | null],
    slaKindCondition: [null as SlaKind | null],
    targetAgentUserId: [null as string | null],
    warningLeadMinutes: [null as number | null],
  });
  private readonly value = toSignal(this.form.valueChanges.pipe(startWith(this.form.getRawValue())), {
    initialValue: this.form.getRawValue(),
  });
  protected readonly trigger = computed(() => this.value().trigger ?? 'TicketCreated');
  protected readonly action = computed(() => this.value().action ?? 'AssignToAgent');
  protected readonly availableActions = computed(() => ACTIONS_BY_TRIGGER[this.trigger()]);
  protected readonly activeAgents = computed(() => this.agents().filter(a => a.isActive));

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    forkJoin({
      rules: this.api.listRules(),
      agents: this.agentsApi.list().pipe(catchError(() => of([] as Agent[]))),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ rules, agents }) => {
          this.rules.set(rules);
          this.agents.set(agents);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.loadError.set(describeHttpError(error, 'Could not load automation rules.'));
          this.loading.set(false);
        },
      });
  }

  protected newRule(): void {
    this.form.reset({
      name: '',
      isEnabled: true,
      trigger: 'TicketCreated',
      action: 'AssignToAgent',
      sortOrder: 10,
      categoryCondition: null,
      priorityCondition: null,
      assignedCondition: null,
      slaKindCondition: null,
      targetAgentUserId: null,
      warningLeadMinutes: null,
    });
    this.saveError.set(null);
    this.editing.set('');
  }

  protected edit(rule: AutomationRule): void {
    this.form.reset({
      name: rule.name,
      isEnabled: rule.isEnabled,
      trigger: rule.trigger,
      action: rule.action,
      sortOrder: rule.sortOrder,
      categoryCondition: rule.categoryCondition,
      priorityCondition: rule.priorityCondition,
      assignedCondition: rule.assignedCondition,
      slaKindCondition: rule.slaKindCondition,
      targetAgentUserId: rule.targetAgentUserId,
      warningLeadMinutes: rule.warningLeadMinutes,
    });
    this.saveError.set(null);
    this.editing.set(rule.id);
  }

  protected cancel(): void {
    this.editing.set(null);
    this.saveError.set(null);
  }

  /** Picking a trigger resets the action when the current one is not valid for it (display convenience; the server validates). */
  protected onTriggerChange(trigger: AutomationTrigger): void {
    const actions = ACTIONS_BY_TRIGGER[trigger];
    const patch: Partial<ReturnType<typeof this.form.getRawValue>> = {};
    if (!actions.includes(this.form.controls.action.value)) {
      patch.action = actions[0];
    }
    if (trigger === 'TicketCreated') {
      patch.assignedCondition = null;
      patch.slaKindCondition = null;
      patch.warningLeadMinutes = null;
    } else {
      patch.targetAgentUserId = null;
      if (trigger !== 'SlaWarningDue') patch.warningLeadMinutes = null;
    }
    this.form.patchValue(patch);
  }

  protected onActionChange(action: AutomationAction): void {
    if (action !== 'AssignToAgent') {
      this.form.patchValue({ targetAgentUserId: null });
    }
  }

  protected conditions(rule: AutomationRule): string {
    const parts: string[] = [];
    if (rule.categoryCondition) parts.push(`Category ${rule.categoryCondition}`);
    if (rule.priorityCondition) parts.push(`Priority ${rule.priorityCondition}`);
    if (rule.assignedCondition !== null) parts.push(rule.assignedCondition ? 'Assigned' : 'Unassigned');
    if (rule.slaKindCondition) parts.push(rule.slaKindCondition === 'FirstResponse' ? 'First response' : 'Resolution');
    return parts.length > 0 ? parts.join(' · ') : 'Any ticket';
  }

  protected parameters(rule: AutomationRule): string {
    if (rule.action === 'AssignToAgent') return `Agent: ${rule.targetAgentName ?? 'unknown'}`;
    if (rule.trigger === 'SlaWarningDue') return `${rule.warningLeadMinutes} business min before due`;
    return '';
  }

  protected save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving()) {
      return;
    }
    const v = this.form.getRawValue();
    const body: AutomationRuleRequest = {
      name: v.name.trim(),
      isEnabled: v.isEnabled,
      trigger: v.trigger,
      action: v.action,
      sortOrder: Number(v.sortOrder),
      categoryCondition: v.categoryCondition,
      priorityCondition: v.priorityCondition,
      assignedCondition: v.trigger === 'TicketCreated' ? null : v.assignedCondition,
      slaKindCondition: v.trigger === 'TicketCreated' ? null : v.slaKindCondition,
      targetAgentUserId: v.action === 'AssignToAgent' ? v.targetAgentUserId : null,
      warningLeadMinutes: v.trigger === 'SlaWarningDue' && v.warningLeadMinutes !== null ? Number(v.warningLeadMinutes) : null,
    };
    const id = this.editing();
    this.saving.set(true);
    this.saveError.set(null);
    (id ? this.api.updateRule(id, body) : this.api.createRule(body)).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.saving.set(false);
        this.editing.set(null);
        this.snackBar.open('Automation rule saved.', undefined, { duration: 3000 });
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.saveError.set(describeSaveError(error, 'Could not save the rule.'));
      },
    });
  }
}
