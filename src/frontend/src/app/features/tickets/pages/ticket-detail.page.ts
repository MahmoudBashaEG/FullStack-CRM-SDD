import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TicketsService } from '../data-access/tickets.service';
import { TicketHistoryComponent } from '../components/ticket-history.component';
import { Page, TicketDetail, TicketHistory } from '../models/ticket.models';
import { ticketMessages } from '../models/ticket-messages';
import { switchMap, catchError, of } from 'rxjs';

@Component({ imports: [DatePipe, TicketHistoryComponent], template: `
  @if (loading()) { <p role="status">{{ messages.loading }}</p> }
  @if (error()) { <h1>Ticket information</h1><p role="alert">{{ error() }}</p><button (click)="reload()">Retry ticket</button> }
  @if (ticket(); as detail) {
    <h1>{{ detail.reference }}</h1><h2 dir="auto">{{ detail.subject }}</h2><p class="description" dir="auto">{{ detail.description }}</p>
    <dl><dt>Category</dt><dd>{{ detail.category.displayName }} @if (!detail.category.isAvailable) { <span>(inactive)</span> }</dd>
    <dt>Priority</dt><dd>{{ detail.priority }}</dd><dt>Status</dt><dd>{{ detail.status === 'InProgress' ? 'In Progress' : detail.status }}</dd>
    <dt>Assigned agent</dt><dd>{{ detail.assignedAgent?.displayName ?? 'Unassigned' }} @if (detail.assignedAgent && !detail.assignedAgent.isEligible) { <span>(inactive)</span> }</dd>
    <dt>Created</dt><dd><time [attr.datetime]="detail.createdAtUtc">{{ detail.createdAtUtc | date:'medium' }}</time></dd>
    <dt>Last change</dt><dd><time [attr.datetime]="detail.updatedAtUtc">{{ detail.updatedAtUtc | date:'medium' }}</time></dd></dl>
    <app-ticket-history [page]="history()" [loading]="historyLoading()" [error]="historyError()" (requested)="loadHistory($event)" />
  }`, styles: `.description {white-space:pre-wrap;overflow-wrap:anywhere;} dl{display:grid;grid-template-columns:minmax(7rem, 1fr) 3fr;gap:.5rem;} dd{margin:0;overflow-wrap:anywhere;} h1{overflow-wrap:anywhere;}` })
export class TicketDetailPage implements OnInit {
  private readonly service = inject(TicketsService); private readonly route = inject(ActivatedRoute); private readonly destroy = inject(DestroyRef);
  private reference = ''; readonly messages = ticketMessages;
  readonly ticket = signal<TicketDetail | null>(null); readonly loading = signal(true); readonly error = signal('');
  readonly history = signal<Page<TicketHistory> | null>(null); readonly historyLoading = signal(false); readonly historyError = signal('');
  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroy), switchMap(params => {
      this.reference = params.get('reference') ?? ''; this.loading.set(true); this.error.set(''); this.ticket.set(null); this.history.set(null);
      return this.service.detail(this.reference).pipe(catchError((failure: HttpErrorResponse) => { this.handleFailure(failure); return of(null); }));
    })).subscribe(detail => { this.loading.set(false); if (detail) { this.ticket.set(detail); this.loadHistory(1); } });
  }
  reload(): void { this.loading.set(true); this.error.set(''); this.service.detail(this.reference).subscribe({ next: detail => { this.ticket.set(detail); this.loading.set(false); this.loadHistory(1); }, error: error => this.handleFailure(error) }); }
  loadHistory(page: number): void {
    const requestedReference = this.reference;
    this.historyLoading.set(true); this.historyError.set('');
    this.service.history(requestedReference, page).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: history => { if (requestedReference !== this.reference) return; this.history.set(history); this.historyLoading.set(false); }, error: () => { if (requestedReference !== this.reference) return; this.historyLoading.set(false); this.historyError.set(this.messages.historyFailed); } });
  }
  private handleFailure(error: HttpErrorResponse): void { this.loading.set(false); this.error.set(error.status === 404 ? this.messages.missing : error.status === 403 ? this.messages.forbidden : error.status === 401 ? this.messages.unauthenticated : this.messages.failed); }
}
