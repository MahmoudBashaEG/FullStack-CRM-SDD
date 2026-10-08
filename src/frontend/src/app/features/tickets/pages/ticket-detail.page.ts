import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TicketsService } from '../data-access/tickets.service';
import { TicketHistoryComponent } from '../components/ticket-history.component';
import { Page, TicketDetail, TicketHistory, ReferenceChoice } from '../models/ticket.models';
import { TicketEditForm } from '../components/ticket-edit-form.component';
import { TicketAssignment } from '../components/ticket-assignment.component';
import { TicketConflict } from '../components/ticket-conflict.component';
import { TicketEditSession } from '../services/ticket-edit-session.service';
import { TicketStatusComponent } from '../components/ticket-status.component';
import { ticketStatusLabels } from '../models/ticket-lifecycle';
import { ticketMessages } from '../models/ticket-messages';
import { switchMap, catchError, of } from 'rxjs';

@Component({ imports: [DatePipe, TicketHistoryComponent, TicketEditForm, TicketAssignment, TicketConflict, TicketStatusComponent], providers: [TicketEditSession], template: `
  @if (loading()) { <p role="status">{{ messages.loading }}</p> }
  @if (error()) { <h1>Ticket information</h1><p role="alert">{{ error() }}</p><button (click)="reload()">Retry ticket</button> }
  @if (ticket(); as detail) {
    <h1>{{ detail.reference }}</h1><h2 dir="auto">{{ detail.subject }}</h2><p class="description" dir="auto">{{ detail.description }}</p>
    <dl><dt>Category</dt><dd>{{ detail.category.displayName }} @if (!detail.category.isAvailable) { <span>(inactive)</span> }</dd>
    <dt>Priority</dt><dd>{{ detail.priority }}</dd><dt>Status</dt><dd>{{ statusLabels[detail.status] }}</dd>
    <dt>Assigned agent</dt><dd>{{ detail.assignedAgent?.displayName ?? 'Unassigned' }} @if (detail.assignedAgent && !detail.assignedAgent.isEligible) { <span>(inactive)</span> }</dd>
    <dt>Created</dt><dd><time [attr.datetime]="detail.createdAtUtc">{{ detail.createdAtUtc | date:'medium' }}</time></dd>
    <dt>Last change</dt><dd><time [attr.datetime]="detail.updatedAtUtc">{{ detail.updatedAtUtc | date:'medium' }}</time></dd></dl>
    @if (detail.status === 'Closed') { <p>This ticket is closed. Editing and assignment are unavailable until it is reopened.</p> }
    @if (referenceError()) { <p role="alert">{{ referenceError() }}</p><button (click)="loadReferences()">Retry choices</button> }
    <app-ticket-conflict [message]="session.message()" [needsReload]="session.needsReload()" [review]="session.review()" [reviewed]="session.reviewed()" [busy]="session.busy()" (reload)="reloadForReview()" (confirm)="session.confirmReview()" />
    @if (session.seed(); as seed) {
      <app-ticket-edit-form [ticket]="detail" [seed]="seed" [categories]="categories()" [ready]="referencesReady()" [blocked]="session.blocked()" [fieldErrors]="session.fieldErrors()" (save)="session.saveDetails($event)" />
      <app-ticket-assignment [ticket]="detail" [seed]="seed" [agents]="agents()" [ready]="referencesReady()" [blocked]="session.blocked()" (save)="session.saveAssignment($event)" />
      <app-ticket-status [ticket]="detail" [seed]="seed" [blocked]="session.statusBlocked()" (save)="session.saveStatus($event)" />
    }
    <app-ticket-history [page]="history()" [loading]="historyLoading()" [error]="historyError()" (requested)="loadHistory($event)" />
  }`, styles: `.description {white-space:pre-wrap;overflow-wrap:anywhere;} dl{display:grid;grid-template-columns:minmax(7rem, 1fr) 3fr;gap:.5rem;} dd{margin:0;overflow-wrap:anywhere;} h1{overflow-wrap:anywhere;}` })
export class TicketDetailPage implements OnInit {
  readonly statusLabels = ticketStatusLabels;
  readonly session = inject(TicketEditSession);
  private readonly service = inject(TicketsService); private readonly route = inject(ActivatedRoute); private readonly destroy = inject(DestroyRef);
  private reference = ''; readonly messages = ticketMessages;
  readonly ticket = this.session.detail; readonly loading = signal(true); readonly error = signal('');
  readonly categories = signal<ReferenceChoice[]>([]); readonly agents = signal<ReferenceChoice[]>([]); readonly referencesReady = signal(false); readonly referenceError = signal('');
  readonly history = signal<Page<TicketHistory> | null>(null); readonly historyLoading = signal(false); readonly historyError = signal('');
  ngOnInit(): void {
    this.session.saved.pipe(takeUntilDestroyed(this.destroy)).subscribe(() => { this.loadHistory(1); this.loadReferences(); });
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroy), switchMap(params => {
      this.reference = params.get('reference') ?? ''; this.loading.set(true); this.error.set(''); this.session.reset(); this.history.set(null);
      return this.service.detail(this.reference).pipe(catchError((failure: HttpErrorResponse) => { this.handleFailure(failure); return of(null); }));
    })).subscribe(detail => { this.loading.set(false); if (detail) { this.session.accept(detail); this.loadHistory(1); this.loadReferences(); } });
  }
  reload(): void { this.loading.set(true); this.error.set(''); this.service.detail(this.reference).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: detail => { this.session.accept(detail); this.loading.set(false); this.loadHistory(1); this.loadReferences(); }, error: error => this.handleFailure(error) }); }
  reloadForReview(): void { this.session.reload(); this.loadHistory(1); this.loadReferences(); }
  loadReferences(): void {
    const reference = this.reference; this.referencesReady.set(false); this.referenceError.set('');
    let categoriesDone = false; let agentsDone = false;
    const failed = () => { if (reference === this.reference) { this.referenceError.set('Ticket choices could not be loaded. Retry choices.'); this.referencesReady.set(false); } };
    this.service.categories().pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: choices => { if (reference !== this.reference) return; this.categories.set(choices); categoriesDone = true; this.referencesReady.set(agentsDone && !this.referenceError()); }, error: failed });
    this.service.agents().pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: choices => { if (reference !== this.reference) return; this.agents.set(choices); agentsDone = true; this.referencesReady.set(categoriesDone && !this.referenceError()); }, error: failed });
  }
  loadHistory(page: number): void {
    const requestedReference = this.reference;
    this.historyLoading.set(true); this.historyError.set('');
    this.service.history(requestedReference, page).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: history => { if (requestedReference !== this.reference) return; this.history.set(history); this.historyLoading.set(false); }, error: () => { if (requestedReference !== this.reference) return; this.historyLoading.set(false); this.historyError.set(this.messages.historyFailed); } });
  }
  private handleFailure(error: HttpErrorResponse): void { this.loading.set(false); this.error.set(error.status === 404 ? this.messages.missing : error.status === 403 ? this.messages.forbidden : error.status === 401 ? this.messages.unauthenticated : this.messages.failed); }
}
