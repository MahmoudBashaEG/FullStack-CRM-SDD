import { Component, input, output } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { CreateTicket, FALLBACK_CATEGORY, ReferenceChoice, TicketPriority } from '../models/ticket.models';
import { ticketMessages } from '../models/ticket-messages';

const nonblank = (control: AbstractControl): ValidationErrors | null => typeof control.value === 'string' && control.value.trim() ? null : { required: true };
@Component({ selector: 'app-ticket-create-form', imports: [ReactiveFormsModule], template: `
  <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
    <label for="subject">Subject</label><input id="subject" formControlName="subject" aria-required="true" aria-describedby="subject-error" dir="auto">
    <p id="subject-error" class="field-error">@if (form.controls.subject.touched && form.controls.subject.invalid) { {{ messages.requiredSubject }} } {{ fieldErrors()['subject']?.join(' ') }}</p>
    <label for="description">Description</label><textarea id="description" formControlName="description" rows="6" aria-required="true" aria-describedby="description-error" dir="auto"></textarea>
    <p id="description-error" class="field-error">@if (form.controls.description.touched && form.controls.description.invalid) { {{ messages.requiredDescription }} } {{ fieldErrors()['description']?.join(' ') }}</p>
    <label for="category">Category</label><select id="category" formControlName="categoryId" aria-describedby="category-error">
      @for (category of categories(); track category.id) { <option [value]="category.id">{{ category.displayName }}</option> }
    </select><p id="category-error" class="field-error">{{ fieldErrors()['categoryId']?.join(' ') }}</p>
    <label for="priority">Priority</label><select id="priority" formControlName="priority" aria-describedby="priority-error">
      @for (priority of priorities; track priority) { <option [value]="priority">{{ priority }}</option> }
    </select><p id="priority-error" class="field-error">{{ fieldErrors()['priority']?.join(' ') }}</p>
    <button type="submit" [disabled]="busy()">{{ busy() ? 'Saving…' : 'Create ticket' }}</button>
  </form>`, styles: `form { display:grid; gap:.4rem; max-inline-size:46rem; } textarea { resize:vertical; } .field-error { color:var(--error-color); min-block-size:1rem; margin-block:0 .7rem; }` })
export class TicketCreateForm {
  readonly categories = input.required<ReferenceChoice[]>();
  readonly busy = input(false);
  readonly fieldErrors = input<Record<string, string[]>>({});
  readonly save = output<CreateTicket>();
  readonly messages = ticketMessages;
  readonly priorities: TicketPriority[] = ['Low', 'Normal', 'High', 'Urgent'];
  readonly form = new FormGroup({ subject: new FormControl('', { nonNullable: true, validators: [nonblank] }),
    description: new FormControl('', { nonNullable: true, validators: [nonblank] }),
    categoryId: new FormControl(FALLBACK_CATEGORY, { nonNullable: true, validators: [Validators.required] }),
    priority: new FormControl<TicketPriority>('Normal', { nonNullable: true, validators: [Validators.required] }) });
  submit(): void { this.form.markAllAsTouched(); if (this.form.valid && !this.busy()) this.save.emit(this.form.getRawValue()); }
}
