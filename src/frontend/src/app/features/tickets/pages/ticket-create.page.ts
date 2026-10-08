import { Component, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { TicketsService } from '../data-access/tickets.service';
import { TicketCreateForm } from '../components/ticket-create-form.component';
import { CreateTicket, ReferenceChoice } from '../models/ticket.models';
import { ticketMessages } from '../models/ticket-messages';

@Component({ imports: [TicketCreateForm], template: `<h1>Create ticket</h1>
  @if (error()) { <p role="alert">{{ error() }}</p> }
  @if (!ready()) { <p role="status">Loading categories…</p><button (click)="loadCategories()" [disabled]="loading()">Retry categories</button> }
  @if (ready()) { <app-ticket-create-form [categories]="categories()" [busy]="saving()" [fieldErrors]="fieldErrors()" (save)="create($event)" /> }` })
export class TicketCreatePage implements OnInit {
  private readonly service = inject(TicketsService); private readonly router = inject(Router);
  readonly categories = signal<ReferenceChoice[]>([]); readonly loading = signal(false); readonly ready = signal(false);
  readonly saving = signal(false); readonly error = signal(''); readonly fieldErrors = signal<Record<string, string[]>>({});
  ngOnInit(): void { this.loadCategories(); }
  loadCategories(): void {
    this.loading.set(true); this.error.set('');
    this.service.categories().subscribe({ next: values => { this.categories.set(values); this.ready.set(true); this.loading.set(false); }, error: () => { this.error.set('Categories could not be loaded. Try again.'); this.loading.set(false); } });
  }
  create(input: CreateTicket): void {
    if (this.saving()) return;
    this.saving.set(true); this.error.set(''); this.fieldErrors.set({});
    this.service.create(input).subscribe({ next: detail => { this.saving.set(false); void this.router.navigate(['/tickets', detail.reference]); },
      error: (failure: HttpErrorResponse) => {
        this.saving.set(false);
        this.fieldErrors.set(failure.error?.errors ?? {});
        this.error.set(failure.status === 403 ? ticketMessages.forbidden : failure.status === 401 ? ticketMessages.unauthenticated : failure.error?.code === 'category_unavailable' ? ticketMessages.categoryUnavailable : ticketMessages.createFailed);
      } });
  }
}
