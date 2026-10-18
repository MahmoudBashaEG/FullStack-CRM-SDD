import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { catchError, debounceTime, filter, map, of, startWith, switchMap, tap } from 'rxjs';
import { I18n } from '../../core/i18n/i18n';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { describeHttpError } from '../../core/api/describe-http-error';
import { LinkableTicket } from './task-models';
import { TasksApi } from './tasks-api';

/**
 * Autocomplete over GET /api/v1/tasks/linkable-tickets (tickets currently assigned to the task owner).
 * Display aid only: the server re-checks the link under a lock on save. Clears its selection whenever the owner changes.
 */
@Component({
  selector: 'crm-ticket-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatAutocompleteModule, MatIconModule, TranslatePipe],
  template: `
    <mat-form-field appearance="outline" class="picker">
      <mat-label>{{ label() ?? ('tasks.picker.label' | t) }}</mat-label>
      <input
        matInput
        type="search"
        [formControl]="query"
        [matAutocomplete]="auto"
        [placeholder]="'tasks.picker.placeholder' | t" />
      <mat-icon matSuffix>search</mat-icon>
      <mat-autocomplete #auto="matAutocomplete" [displayWith]="display" (optionSelected)="selected.emit($event.option.value)">
        @for (t of options(); track t.id) {
          <mat-option [value]="t">{{ t.subject }} · {{ 'enums.ticketStatus.' + t.status | t }} · {{ 'enums.ticketPriority.' + t.priority | t }}</mat-option>
        } @empty {
          <mat-option disabled>{{ 'tasks.picker.noMatch' | t }}</mat-option>
        }
      </mat-autocomplete>
      @if (lookupError(); as message) {
        <mat-hint class="lookup-error">{{ message }}</mat-hint>
      } @else if (requireOwner() && !ownerUserId()) {
        <mat-hint>{{ 'tasks.picker.selectOwner' | t }}</mat-hint>
      } @else {
        <mat-hint>{{ 'tasks.picker.onlyAssigned' | t }}</mat-hint>
      }
    </mat-form-field>
  `,
  styles: `
    .picker {
      inline-size: 100%;
    }
    .lookup-error {
      color: var(--mat-sys-error);
    }
  `,
})
export class TicketPicker {
  private readonly i18n = inject(I18n);
  private readonly api = inject(TasksApi);

  readonly label = input<string | null>(null);
  readonly ownerUserId = input<string | null>(null);
  /** SuperAdmin forms: the field stays disabled until an owner is chosen. */
  readonly requireOwner = input(false);
  readonly selected = output<LinkableTicket | null>();

  protected readonly query = new FormControl<string | LinkableTicket>('', { nonNullable: true });
  protected readonly options = signal<LinkableTicket[]>([]);
  protected readonly lookupError = signal<string | null>(null);
  private readonly owner = signal<string | null>(null);

  constructor() {
    // Owner changed ⇒ clear the typed text, the options and the parent's selection.
    effect(() => {
      const owner = this.ownerUserId();
      const disabled = this.requireOwner() && !owner;
      untracked(() => {
        this.owner.set(owner);
        this.options.set([]);
        this.query.setValue('', { emitEvent: false });
        this.selected.emit(null);
        if (disabled) {
          this.query.disable({ emitEvent: false });
        } else {
          this.query.enable({ emitEvent: false });
          this.query.setValue(''); // triggers a fresh lookup for the new owner
        }
      });
    });

    this.query.valueChanges.pipe(takeUntilDestroyed()).subscribe(value => {
      if (typeof value === 'string') {
        this.selected.emit(null);
      }
    });

    this.query.valueChanges
      .pipe(
        startWith(''),
        filter((value): value is string => typeof value === 'string'),
        map(value => value.trim()),
        debounceTime(300),
        tap(() => this.lookupError.set(null)),
        switchMap(search => {
          if (this.query.disabled) {
            return of([] as LinkableTicket[]);
          }
          return this.api.linkableTickets(this.owner(), search).pipe(
            catchError((error: HttpErrorResponse) => {
              this.lookupError.set(describeHttpError(error, this.i18n.t('tasks.picker.searchFailed'), this.i18n.t));
              return of([] as LinkableTicket[]);
            }),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe(items => this.options.set(items));
  }

  protected readonly display = (value: LinkableTicket | string | null): string =>
    typeof value === 'string' ? value : (value?.subject ?? '');
}
