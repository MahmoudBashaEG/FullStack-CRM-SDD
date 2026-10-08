import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of, Subject, switchMap } from 'rxjs';
import { TicketsService } from '../data-access/tickets.service';
import { TicketFiltersComponent } from '../components/ticket-filters.component';
import { FilterReference, Page, TicketFilters, TicketListParams, TicketSummary } from '../models/ticket.models';

@Component({ imports: [DatePipe, RouterLink, TicketFiltersComponent], template: `<h1>Tickets</h1>
  <app-ticket-filters [criteria]="filters()" [categories]="categories()" [agents]="agents()" (apply)="applyFilters($event)" />
  @if (referenceError()) { <p role="alert">{{ referenceError() }}</p><button (click)="loadReferences()">Retry reference choices</button> }
  @if (loading()) { <p role="status">Loading tickets…</p> }
  @if (error()) { <p role="alert">{{ error() }}</p><button (click)="reload()">Retry tickets</button> }
  @if (page(); as result) {
    @if (result.items.length === 0) { <p>No tickets match these filters.</p> }
    @else { <div class="table-scroll"><table><caption>Shared support queue</caption><thead><tr><th scope="col">Reference</th><th scope="col">Subject</th><th scope="col">Category</th><th scope="col">Priority</th><th scope="col">Status</th><th scope="col">Assigned agent</th><th scope="col">Last change</th></tr></thead>
      <tbody>@for (ticket of result.items; track ticket.reference) { <tr>
        <td><a [routerLink]="['/tickets', ticket.reference]">{{ ticket.reference }}</a></td><td dir="auto">{{ ticket.subject }}</td>
        <td>{{ ticket.category.displayName }}{{ !ticket.category.isAvailable ? ' (inactive)' : '' }}</td><td>{{ ticket.priority }}</td>
        <td>{{ ticket.status === 'InProgress' ? 'In Progress' : ticket.status }}</td><td>{{ ticket.assignedAgent?.displayName ?? 'Unassigned' }}{{ ticket.assignedAgent && !ticket.assignedAgent.isEligible ? ' (inactive)' : '' }}</td>
        <td><time [attr.datetime]="ticket.updatedAtUtc">{{ ticket.updatedAtUtc | date:'medium' }}</time></td>
      </tr> }</tbody></table></div> }
    <nav aria-label="Ticket pages"><button [disabled]="result.page <= 1 || loading()" (click)="goToPage(result.page - 1)">Previous tickets</button>
      <span>Page {{ result.page }}</span><button [disabled]="!result.hasMore || loading()" (click)="goToPage(result.page + 1)">Next tickets</button></nav>
  }`, styles: `.table-scroll{overflow:auto;}table{border-collapse:collapse;inline-size:100%;}caption{text-align:start;font-weight:600;padding-block:1rem;}th,td{text-align:start;padding:.7rem;border-block-end:1px solid #d7dfe5;vertical-align:top;overflow-wrap:anywhere;}nav{display:flex;gap:1rem;align-items:center;flex-wrap:wrap;margin-block:1rem;}` })
export class TicketListPage implements OnInit {
  private readonly service = inject(TicketsService); private readonly route = inject(ActivatedRoute); private readonly router = inject(Router); private readonly destroy = inject(DestroyRef);
  private readonly requests = new Subject<TicketListParams | null>(); private current: TicketListParams = {}; private invalidQuery = false;
  readonly filters = signal<TicketFilters>({ q: '', status: '', categoryId: '', priority: '', assignment: '' });
  readonly page = signal<Page<TicketSummary> | null>(null); readonly loading = signal(false); readonly error = signal(''); readonly referenceError = signal('');
  private readonly knownCategories = signal<FilterReference[]>([]); private readonly knownAgents = signal<FilterReference[]>([]);
  readonly categories = computed(() => this.withSelected(this.knownCategories(), this.filters().categoryId));
  readonly agents = computed(() => this.withSelected(this.knownAgents(), this.filters().assignment === 'unassigned' ? '' : this.filters().assignment));
  ngOnInit(): void {
    this.requests.pipe(takeUntilDestroyed(this.destroy), switchMap(criteria => {
      if (criteria === null) { this.loading.set(false); this.page.set(null); return of(null); }
      this.loading.set(true); this.error.set(''); this.page.set(null);
      return this.service.list(criteria).pipe(catchError((failure: HttpErrorResponse) => { this.error.set(this.failureMessage(failure)); return of(null); }));
    })).subscribe(page => {
      this.loading.set(false); this.page.set(page);
      if (page) {
        this.mergeReferences(this.knownCategories, page.items.map(item => ({ ...item.category, inactive: !item.category.isAvailable })));
        this.mergeReferences(this.knownAgents, page.items.flatMap(item => item.assignedAgent ? [{ ...item.assignedAgent, inactive: !item.assignedAgent.isEligible }] : []));
      }
    });
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroy)).subscribe(params => {
      this.filters.set({ q: params.get('q') ?? '', status: params.get('status') ?? '', categoryId: params.get('categoryId') ?? '', priority: params.get('priority') ?? '', assignment: params.get('assignment') ?? '' });
      this.invalidQuery = params.keys.some(key => !['q', 'status', 'categoryId', 'priority', 'assignment', 'page', 'pageSize'].includes(key) || params.getAll(key).length !== 1);
      if (this.invalidQuery) { this.error.set('Provide one supported value for each filter.'); this.requests.next(null); return; }
      const paging = (key: string, fallback: number) => { const raw = params.get(key); return raw === null ? fallback : /^[+-]?\d+$/.test(raw.trim()) ? Number(raw) : raw; };
      this.current = { page: paging('page', 1), pageSize: paging('pageSize', 25) };
      for (const key of ['q', 'status', 'categoryId', 'priority', 'assignment'] as const) if (params.has(key)) this.current[key] = params.get(key)!;
      this.reload();
    });
    this.loadReferences();
  }
  loadReferences(): void {
    this.referenceError.set('');
    this.service.categories().pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: choices => this.mergeReferences(this.knownCategories, choices), error: () => this.referenceError.set('Reference choices could not be loaded. Existing ticket information remains readable.') });
    this.service.agents().pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: choices => this.mergeReferences(this.knownAgents, choices), error: () => this.referenceError.set('Reference choices could not be loaded. Existing ticket information remains readable.') });
  }
  reload(): void { if (!this.invalidQuery) this.requests.next(this.current); }
  applyFilters(filters: TicketFilters): void {
    const size = Number(this.current.pageSize);
    const query: Record<string, string | number> = { page: 1, pageSize: Number.isInteger(size) && size >= 1 && size <= 100 ? size : 25 };
    for (const [key, value] of Object.entries(filters)) if (value !== '') query[key] = value;
    void this.router.navigate([], { relativeTo: this.route, queryParams: query });
  }
  goToPage(page: number): void { void this.router.navigate([], { relativeTo: this.route, queryParams: { ...this.current, page } }); }
  private mergeReferences(target: { (): FilterReference[]; set(value: FilterReference[]): void }, values: FilterReference[]): void {
    const merged = new Map(target().map(value => [value.id, value])); for (const value of values) merged.set(value.id, value);
    target.set([...merged.values()].sort((left, right) => left.displayName.localeCompare(right.displayName)));
  }
  private withSelected(values: FilterReference[], id: string): FilterReference[] { return id && !values.some(value => value.id === id) ? [...values, { id, displayName: id }] : values; }
  private failureMessage(failure: HttpErrorResponse): string {
    return failure.status === 400 ? 'Some filters are invalid. Review or clear them and try again.' : failure.status === 401 ? 'Sign in to access the support queue.' : failure.status === 403 ? 'You do not have shared queue access.' : 'Tickets could not be loaded. Try again.';
  }
}
