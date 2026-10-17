import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { Customer, CustomerListParams } from './customer-models';
import { CustomersApi } from './customers-api';

@Component({
  selector: 'crm-customer-list-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    StatusBadge,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './customer-list-page.html',
  styleUrl: './customer-list-page.scss',
})
export class CustomerListPage {
  private readonly api = inject(CustomersApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<CustomerListParams>();

  protected readonly columns = ['name', 'email', 'phone', 'company', 'type', 'actions'];
  protected readonly pageSizeOptions = [10, 20, 50, 100];
  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly customers = signal<Customer[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1); // one-based, as the API
  protected readonly pageSize = signal(20);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.requests
      .pipe(
        // A newer search/page cancels the older request. Errors are caught per request (inside switchMap),
        // so a failed request does not complete the outer stream and later searches/pages still load.
        switchMap(params =>
          this.api.list(params).pipe(
            catchError((error: HttpErrorResponse) => {
              this.error.set(describeHttpError(error, 'Could not load customers.'));
              this.loading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(result => {
        this.customers.set(result.items);
        this.totalCount.set(result.totalCount);
        this.page.set(result.page);
        this.pageSize.set(result.pageSize);
        this.loading.set(false);
      });

    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load(1));

    this.load(1);
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.load(event.pageIndex + 1);
  }

  private load(page: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.requests.next({ page, pageSize: this.pageSize(), search: this.searchControl.value });
  }
}
