import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { EMPTY, Subject, catchError, map, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { Translate, englishT } from '../../core/i18n/translate-fn';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { TicketActivity, TicketStatus, statusLabel } from './ticket-models';
import { TicketsApi } from './tickets-api';

const PAGE_SIZE = 20;

export function describeActivity(a: TicketActivity, t: Translate = englishT): { title: string; detail: string | null } {
  const value = (v: string | null): string =>
    v !== null && (a.field === 'Category' || a.field === 'Priority') ? t(`enums.ticket${a.field}.${v}`) : String(v);
  switch (a.type) {
    case 'Created':
      return {
        title: t('tickets.activity.created'),
        detail: a.toAgentName ? t('tickets.activity.assignedTo', { name: a.toAgentName }) : t('tickets.activity.unassigned'),
      };
    case 'FieldChanged':
      return a.field === 'Description'
        ? { title: t('tickets.activity.descriptionChanged'), detail: null }
        : {
            title: t('tickets.activity.fieldChanged', { field: a.field ? t(`tickets.fields.${a.field}`) : '' }),
            detail: `${value(a.oldValue)} → ${value(a.newValue)}`,
          };
    case 'AssigneeChanged':
      if (a.isAutomatic) return { title: t('tickets.activity.assignedAutomatically'), detail: a.reason ?? t('tickets.activity.to', { name: a.toAgentName ?? '' }) };
      if (!a.fromAgentName) return { title: t('tickets.activity.assigned'), detail: t('tickets.activity.to', { name: a.toAgentName ?? '' }) };
      if (!a.toAgentName) return { title: t('tickets.activity.unassigned'), detail: t('tickets.activity.was', { name: a.fromAgentName }) };
      return { title: t('tickets.activity.reassigned'), detail: `${a.fromAgentName} → ${a.toAgentName}` };
    case 'StatusChanged':
      return {
        title: a.oldValue === 'Closed' && a.newValue === 'Open' ? t('tickets.activity.reopened') : t('tickets.activity.statusChanged'),
        detail: `${statusLabel(a.oldValue as TicketStatus, t)} → ${statusLabel(a.newValue as TicketStatus, t)}`,
      };
    case 'Escalated':
      return { title: a.isAutomatic ? t('tickets.activity.escalatedAutomatically') : t('tickets.activity.escalated'), detail: a.reason };
    case 'EscalationCleared':
      return { title: t('tickets.activity.escalationCleared'), detail: null };
    case 'EscalationAutoCleared':
      return { title: t('tickets.activity.escalationAutoCleared'), detail: t('tickets.activity.ticketResolved') };
    case 'FirstResponseRecorded':
      return { title: t('tickets.activity.firstResponseRecorded'), detail: t('tickets.activity.manualAck') };
  }
}

@Component({
  selector: 'crm-ticket-activity-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizedDatePipe, TranslatePipe, ErrorState, MatButtonModule, MatProgressBarModule],
  templateUrl: './ticket-activity-list.html',
  styleUrl: './ticket-activity-list.scss',
})
export class TicketActivityList {
  private readonly api = inject(TicketsApi);
  private readonly i18n = inject(I18n);
  private readonly destroyRef = inject(DestroyRef);

  readonly ticketId = input.required<string>();
  /** Parent increments after every successful change ⇒ reload from page 1. */
  readonly refresh = input(0);

  protected readonly items = signal<TicketActivity[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly hasMore = computed(() => this.items().length < this.totalCount());
  /** True once a page has arrived successfully — nothing flashes before data. */
  protected readonly loaded = signal(false);
  /**
   * Claim "earlier activity was not recorded" only when it is true: the oldest page is loaded (no more pages) and it has
   * no "Created" event ⇒ the ticket pre-dates tracking (TICK-001). Hidden while loading, on error, before the first page,
   * and while older pages remain.
   */
  protected readonly showTrackingNote = computed(
    () => this.loaded() && !this.loading() && !this.error() && !this.hasMore() && !this.items().some(a => a.type === 'Created'),
  );
  protected describe(a: TicketActivity): { title: string; detail: string | null } {
    return describeActivity(a, this.i18n.t);
  }

  private readonly requests = new Subject<{ id: string; page: number }>();
  private nextPage = 1;

  constructor() {
    this.requests
      .pipe(
        // A newer request cancels the older one; errors are caught per request so later loads still work.
        switchMap(r =>
          this.api.activity(r.id, r.page, PAGE_SIZE).pipe(
            map(result => ({ r, result })),
            catchError((error: HttpErrorResponse) => {
              this.error.set(describeHttpError(error, this.i18n.t('tickets.activity.loadError'), this.i18n.t));
              this.loading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(({ r, result }) => {
        if (r.page === 1) {
          this.items.set(result.items);
        } else {
          const known = new Set(this.items().map(a => a.id)); // events appended meanwhile shift offsets
          this.items.update(current => [...current, ...result.items.filter(a => !known.has(a.id))]);
        }
        this.totalCount.set(result.totalCount);
        this.nextPage = r.page + 1;
        this.loaded.set(true);
        this.loading.set(false);
      });

    effect(() => {
      const id = this.ticketId();
      this.refresh();
      untracked(() => this.load(id, 1));
    });
  }

  protected loadMore(): void {
    this.load(this.ticketId(), this.nextPage);
  }

  protected retry(): void {
    this.load(this.ticketId(), this.items().length === 0 ? 1 : this.nextPage);
  }

  private load(id: string, page: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.requests.next({ id, page });
  }
}
