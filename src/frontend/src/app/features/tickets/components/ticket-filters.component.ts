import { Component, effect, input, output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { FilterReference, TicketFilters } from '../models/ticket.models';

@Component({ selector: 'app-ticket-filters', imports: [ReactiveFormsModule], template: `
  <form [formGroup]="form" (ngSubmit)="apply.emit(form.getRawValue())">
    <label>Search tickets<input formControlName="q" type="search" placeholder="Reference or subject" dir="auto"></label>
    <label>Status filter<select formControlName="status"><option value="">All statuses</option>
      @for (status of statuses; track status) { <option [value]="status">{{ status === 'InProgress' ? 'In Progress' : status }}</option> }</select></label>
    <label>Category filter<select formControlName="categoryId"><option value="">All categories</option>
      @for (category of categories(); track category.id) { <option [value]="category.id">{{ category.displayName }}{{ category.inactive ? ' (inactive)' : '' }}</option> }</select></label>
    <label>Priority filter<select formControlName="priority"><option value="">All priorities</option>
      @for (priority of priorities; track priority) { <option [value]="priority">{{ priority }}</option> }</select></label>
    <label>Assignment filter<select formControlName="assignment"><option value="">All assignments</option><option value="unassigned">Unassigned</option>
      @for (agent of agents(); track agent.id) { <option [value]="agent.id">{{ agent.displayName }}{{ agent.inactive ? ' (inactive)' : '' }}</option> }</select></label>
    <div><button type="submit">Apply filters</button> <button type="button" (click)="clearFilters()">Clear filters</button></div>
  </form>`, styles: `form{display:grid;grid-template-columns:repeat(auto-fit,minmax(12rem,1fr));gap:1rem;margin-block:1rem;}label{display:grid;gap:.3rem;}input,select{inline-size:100%;}div{align-self:end;}` })
export class TicketFiltersComponent {
  readonly criteria = input.required<TicketFilters>(); readonly categories = input<FilterReference[]>([]); readonly agents = input<FilterReference[]>([]);
  readonly apply = output<TicketFilters>();
  readonly statuses = ['Open', 'InProgress', 'Resolved', 'Closed']; readonly priorities = ['Low', 'Normal', 'High', 'Urgent'];
  readonly form = new FormGroup({ q: new FormControl('', { nonNullable: true }), status: new FormControl('', { nonNullable: true }),
    categoryId: new FormControl('', { nonNullable: true }), priority: new FormControl('', { nonNullable: true }), assignment: new FormControl('', { nonNullable: true }) });
  constructor() { effect(() => this.form.patchValue(this.criteria(), { emitEvent: false })); }
  clearFilters(): void { const empty = { q: '', status: '', categoryId: '', priority: '', assignment: '' }; this.form.reset(empty); this.apply.emit(empty); }
}
