import { Component, effect, input, output, untracked } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ReferenceChoice, TicketDetail, TicketDetailsPatch, TicketPriority } from '../models/ticket.models';
const nonblank = (control: AbstractControl): ValidationErrors | null => typeof control.value === 'string' && control.value.trim() ? null : { required: true };

@Component({ selector: 'app-ticket-edit-form', imports: [ReactiveFormsModule], template: `<section aria-labelledby="edit-heading"><h2 id="edit-heading">Edit ticket information</h2>
  <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
    <fieldset [disabled]="ticket().status === 'Closed'" aria-labelledby="edit-heading">
    <label for="edit-subject">Subject</label><input id="edit-subject" formControlName="subject" dir="auto" aria-required="true" aria-describedby="edit-subject-error">
    <p id="edit-subject-error">@if (form.controls.subject.touched && form.controls.subject.invalid) { Subject is required. } {{ fieldErrors()['subject']?.join(' ') }}</p>
    <label for="edit-description">Description</label><textarea id="edit-description" formControlName="description" dir="auto" rows="5" aria-required="true" aria-describedby="edit-description-error"></textarea>
    <p id="edit-description-error">@if (form.controls.description.touched && form.controls.description.invalid) { Description is required. } {{ fieldErrors()['description']?.join(' ') }}</p>
    <label for="edit-category">Category</label><select id="edit-category" formControlName="categoryId">
      @if (!ticket().category.isAvailable && !hasCategory(ticket().category.id)) { <option [value]="ticket().category.id">{{ ticket().category.displayName }} (inactive, retained)</option> }
      @for (category of categories(); track category.id) { <option [value]="category.id">{{ category.displayName }}</option> }
    </select><p>{{ fieldErrors()['categoryId']?.join(' ') }}</p>
    <label for="edit-priority">Priority</label><select id="edit-priority" formControlName="priority">@for (priority of priorities; track priority) { <option [value]="priority">{{ priority }}</option> }</select>
    <p>{{ fieldErrors()['priority']?.join(' ') }}</p>
    @if (selectionError) { <p role="alert">{{ selectionError }}</p> }
    <button [disabled]="blocked() || ticket().status === 'Closed' || !ready()" type="submit">Save ticket information</button>
    </fieldset>
  </form></section>`, styles: `form{max-inline-size:46rem;}fieldset{display:grid;gap:.4rem;border:0;padding:0;margin:0;min-inline-size:0;} p{color:var(--error-color);margin-block:0 .7rem;} textarea{resize:vertical;}` })
export class TicketEditForm {
  readonly ticket = input.required<TicketDetail>(); readonly seed = input.required<TicketDetail>();
  readonly categories = input<ReferenceChoice[]>([]); readonly ready = input(false); readonly blocked = input(false); readonly fieldErrors = input<Record<string, string[]>>({});
  readonly save = output<TicketDetailsPatch>(); readonly priorities: TicketPriority[] = ['Low', 'Normal', 'High', 'Urgent']; selectionError = '';
  readonly form = new FormGroup({ subject: new FormControl('', { nonNullable: true, validators: [nonblank] }), description: new FormControl('', { nonNullable: true, validators: [nonblank] }), categoryId: new FormControl('', { nonNullable: true, validators: [Validators.required] }), priority: new FormControl<TicketPriority>('Normal', { nonNullable: true }) });
  constructor() { effect(() => { const seed = this.seed(); untracked(() => { this.form.reset({ subject: seed.subject, description: seed.description, categoryId: seed.category.id, priority: seed.priority }); this.selectionError = ''; }); }); }
  hasCategory(id: string): boolean { return this.categories().some(choice => choice.id === id); }
  submit(): void {
    this.form.markAllAsTouched(); this.selectionError = '';
    if (!this.form.valid || this.blocked() || !this.ready() || this.ticket().status === 'Closed') return;
    const value = this.form.getRawValue();
    if (value.categoryId !== this.ticket().category.id && !this.hasCategory(value.categoryId)) { this.selectionError = 'Choose an available category for a new selection.'; return; }
    const patch: TicketDetailsPatch = {};
    // Preserve dirty-field intent across reload; resubmission is always an explicit action.
    if (this.form.controls.subject.dirty || value.subject !== this.seed().subject) patch.subject = value.subject;
    if (this.form.controls.description.dirty || value.description !== this.seed().description) patch.description = value.description;
    if (this.form.controls.categoryId.dirty || value.categoryId !== this.seed().category.id) patch.categoryId = value.categoryId;
    if (this.form.controls.priority.dirty || value.priority !== this.seed().priority) patch.priority = value.priority;
    this.save.emit(Object.keys(patch).length ? patch : { subject: this.ticket().subject });
  }
}
