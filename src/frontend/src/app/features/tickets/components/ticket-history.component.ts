import { Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Page, TicketHistory, ReferenceChoice, TicketStatus } from '../models/ticket.models';
import { ticketStatusLabels } from '../models/ticket-lifecycle';

@Component({ selector: 'app-ticket-history', imports: [DatePipe], template: `<section aria-labelledby="history-heading"><h2 id="history-heading">Ticket history</h2>
  @if (loading()) { <p role="status">Loading history…</p> }
  @if (error()) { <p role="alert">{{ error() }}</p><button (click)="requested.emit(retryPage() ?? page()?.page ?? 1)">Retry history</button> }
  @if (page(); as history) {
    @if (history.items.length === 0) { <p>No history entries.</p> }
    <ol>@for (entry of history.items; track entry.id) { <li>
      <h3>{{ entry.kind }} — revision {{ entry.revision }}</h3>
      <p>{{ entry.actor.displayName }} · <time [attr.datetime]="entry.occurredAtUtc">{{ entry.occurredAtUtc | date:'medium' }}</time></p>
      @if (entry.kind === 'Created') {
      <dl><dt>Reference</dt><dd>{{ entry.newValue.reference }}</dd><dt>Subject</dt><dd dir="auto">{{ entry.newValue.subject }}</dd>
      <dt>Description</dt><dd class="text" dir="auto">{{ entry.newValue.description }}</dd><dt>Category</dt><dd>{{ entry.newValue.category.displayName }}</dd>
      <dt>Priority</dt><dd>{{ entry.newValue.priority }}</dd><dt>Status</dt><dd>{{ display(entry.newValue.status, 'status') }}</dd><dt>Assigned agent</dt><dd>Unassigned</dd></dl>
      } @else {
        <dl><dt>Changed field</dt><dd>{{ fieldLabel(entry.field) }}</dd><dt>Previous value</dt><dd class="text" dir="auto">{{ display(entry.previousValue, entry.field) }}</dd>
        <dt>New value</dt><dd class="text" dir="auto">{{ display(entry.newValue, entry.field) }}</dd></dl>
      }
    </li> }</ol>
    <nav aria-label="History pages"><button [disabled]="history.page <= 1 || loading()" (click)="requested.emit(history.page - 1)">Previous history</button>
    <span>Page {{ history.page }}</span><button [disabled]="!history.hasMore || loading()" (click)="requested.emit(history.page + 1)">Next history</button></nav>
  }
  </section>`, styles: `li { margin-block-end:1.5rem; } dl { display:grid; grid-template-columns:minmax(7rem, 1fr) 3fr; gap:.4rem; } dd { margin:0; overflow-wrap:anywhere; } .text {white-space:pre-wrap;} nav {display:flex;gap:1rem;flex-wrap:wrap;}` })
export class TicketHistoryComponent {
  display(value: string | ReferenceChoice | null, field?: string): string { return value === null ? 'Unassigned' : typeof value === 'string' ? field === 'status' ? ticketStatusLabels[value as TicketStatus] ?? value : value : value.displayName; }
  fieldLabel(field: string): string { return ({ subject: 'Subject', description: 'Description', category: 'Category', priority: 'Priority', assignedAgent: 'Assigned agent', status: 'Status' } as Record<string, string>)[field] ?? field; }
  readonly page = input<Page<TicketHistory> | null>(null); readonly loading = input(false); readonly error = input('');
  readonly retryPage = input<number | null>(null);
  readonly requested = output<number>();
}
