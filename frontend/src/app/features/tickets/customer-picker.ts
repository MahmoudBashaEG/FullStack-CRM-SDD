import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { catchError, debounceTime, distinctUntilChanged, filter, map, of, startWith, switchMap, tap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { I18n } from '../../core/i18n/i18n';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { Customer } from '../customers/customer-models';
import { CustomersApi } from '../customers/customers-api';

/**
 * Autocomplete over GET /api/v1/customers?search= (registered and contact-only customers).
 * Emits the chosen customer, or null as soon as the user types again (the selection is cleared).
 */
@Component({
  selector: 'crm-customer-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatAutocompleteModule, MatIconModule, TranslatePipe],
  template: `
    <mat-form-field appearance="outline" class="picker">
      <mat-label>{{ label() ?? ('tickets.picker.customer' | t) }}</mat-label>
      <input
        matInput
        type="search"
        [formControl]="query"
        [matAutocomplete]="auto"
        [placeholder]="'tickets.picker.placeholder' | t" />
      <mat-icon matSuffix>person_search</mat-icon>
      <mat-autocomplete #auto="matAutocomplete" [displayWith]="display" (optionSelected)="selected.emit($event.option.value)">
        @for (c of options(); track c.id) {
          <mat-option [value]="c">{{ c.firstName }} {{ c.lastName }} · <span class="ltr-value">{{ c.email }}</span></mat-option>
        } @empty {
          <mat-option disabled>{{ 'tickets.picker.noMatch' | t }}</mat-option>
        }
      </mat-autocomplete>
      @if (lookupError(); as message) {
        <mat-hint class="lookup-error">{{ message }}</mat-hint>
      } @else if (hint()) {
        <mat-hint>{{ hint() }}</mat-hint>
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
export class CustomerPicker {
  private readonly api = inject(CustomersApi);
  private readonly i18n = inject(I18n);

  readonly label = input<string>();
  readonly hint = input<string>();
  readonly selected = output<Customer | null>();

  protected readonly query = new FormControl<string | Customer>('', { nonNullable: true });
  protected readonly options = signal<Customer[]>([]);
  protected readonly lookupError = signal<string | null>(null);

  constructor() {
    // Typing after a selection clears it, so the parent never keeps a customer the field no longer shows.
    this.query.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(value => {
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
        distinctUntilChanged(),
        tap(() => this.lookupError.set(null)),
        // Newest 10 matches; a newer term cancels the older request; errors stay per request.
        switchMap(search =>
          this.api.list({ page: 1, pageSize: 10, search }).pipe(
            map(page => page.items),
            catchError((error: HttpErrorResponse) => {
              this.lookupError.set(describeHttpError(error, this.i18n.t('tickets.picker.searchError'), this.i18n.t));
              return of([] as Customer[]);
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe(items => this.options.set(items));
  }

  protected readonly display = (value: Customer | string | null): string =>
    typeof value === 'string' ? value : value ? `${value.firstName} ${value.lastName}` : '';
}
