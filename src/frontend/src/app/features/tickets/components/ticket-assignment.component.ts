import { Component, effect, input, output, untracked } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ReferenceChoice, TicketDetail } from '../models/ticket.models';
@Component({ selector: 'app-ticket-assignment', imports: [ReactiveFormsModule], template: `<section aria-labelledby="assignment-heading"><h2 id="assignment-heading">Assign ticket</h2>
  <form [formGroup]="form" (ngSubmit)="submit()">
    <label for="assignment-agent">Assigned agent</label><select id="assignment-agent" formControlName="agent">
      <option value="">Unassigned</option>
      @if (ticket().assignedAgent; as current) { @if (!current.isEligible && !hasAgent(current.id)) { <option [value]="current.id">{{ current.displayName }} (inactive, retained)</option> } }
      @for (choice of agents(); track choice.id) { <option [value]="choice.id">{{ choice.displayName }}</option> }
    </select>
    @if (selectionError) { <p role="alert">{{ selectionError }}</p> }
    <button type="submit" [disabled]="blocked() || ticket().status === 'Closed' || !ready()">Save assignment</button>
  </form></section>`, styles: `form{display:grid;gap:.5rem;max-inline-size:46rem;}` })
export class TicketAssignment {
  readonly ticket = input.required<TicketDetail>(); readonly seed = input.required<TicketDetail>(); readonly agents = input<ReferenceChoice[]>([]);
  readonly blocked = input(false); readonly ready = input(false); readonly save = output<string | null>();
  readonly agent = new FormControl('', { nonNullable: true }); selectionError = '';
  readonly form = new FormGroup({ agent: this.agent });
  constructor() { effect(() => { const seed = this.seed(); untracked(() => { this.agent.reset(seed.assignedAgent?.id ?? ''); this.selectionError = ''; }); }); }
  hasAgent(id: string): boolean { return this.agents().some(choice => choice.id === id); }
  submit(): void {
    this.selectionError = ''; if (this.blocked() || !this.ready() || this.ticket().status === 'Closed') return;
    const id = this.agent.value || null;
    if (id !== null && id !== this.ticket().assignedAgent?.id && !this.hasAgent(id)) { this.selectionError = 'Choose an eligible agent for a new assignment.'; return; }
    this.save.emit(id);
  }
}
