import { Component, effect, input, output, untracked } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { TicketDetail, TicketStatus } from '../models/ticket.models';
import { allowedNextStatuses, ticketStatusLabels } from '../models/ticket-lifecycle';

@Component({ selector: 'app-ticket-status', imports: [ReactiveFormsModule], template: `<section aria-labelledby="status-heading"><h2 id="status-heading">Ticket lifecycle</h2>
  <form [formGroup]="form" (ngSubmit)="submit()"><label for="ticket-status">Ticket status</label><select id="ticket-status" formControlName="status">
    <option value="">Choose a status</option>@for (status of choices(); track $index) { <option [value]="status">{{ labels[status] }}{{ status === 'Open' ? ' (reopen)' : '' }}</option> }
    @if (status.value && !isAllowed(status.value)) { <option [value]="status.value" disabled>{{ labels[status.value] }} (draft; no longer allowed)</option> }
  </select><p aria-live="polite">{{ selectionError }}</p><button [disabled]="blocked()" type="submit">Change ticket status</button></form>
</section>`, styles: `form{display:grid;gap:.5rem;max-inline-size:46rem;}p{color:var(--error-color);margin-block:0;}` })
export class TicketStatusComponent {
  readonly ticket = input.required<TicketDetail>(); readonly seed = input.required<TicketDetail>(); readonly blocked = input(false); readonly save = output<TicketStatus>();
  readonly status = new FormControl<TicketStatus | ''>('', { nonNullable: true }); readonly form = new FormGroup({ status: this.status }); readonly labels = ticketStatusLabels;
  selectionError = '';
  constructor() { effect(() => { this.seed(); untracked(() => { this.status.reset(''); this.selectionError = ''; }); }); }
  choices(): readonly TicketStatus[] { return allowedNextStatuses(this.ticket().status); }
  isAllowed(status: TicketStatus): boolean { return this.choices().includes(status); }
  submit(): void {
    this.selectionError = ''; if (this.blocked()) return;
    const status = this.status.value;
    if (!status || !this.isAllowed(status)) { this.selectionError = 'Choose a status allowed from the current ticket status.'; return; }
    this.save.emit(status);
  }
}
