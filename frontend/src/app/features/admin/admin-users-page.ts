import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, debounceTime, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { UserRole } from '../../core/auth/auth-models';
import { I18n } from '../../core/i18n/i18n';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { AdminApi } from './admin-api';
import { AccountListParams, AdminAccount } from './admin-models';
import { AdminSectionNav } from './admin-section-nav';

@Component({
  selector: 'crm-admin-users-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    StatusBadge,
    AdminSectionNav,
    EmptyState,
    ErrorState,
    TranslatePipe,
    MatCardModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './admin-users-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './admin-page.scss'],
})
export class AdminUsersPage {
  private readonly api = inject(AdminApi);
  private readonly i18n = inject(I18n);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<AccountListParams>();

  protected readonly pageSizeOptions = [20, 50, 100];
  protected readonly roles: readonly UserRole[] = ['SuperAdmin', 'Agent', 'Customer'];
  protected readonly filters = inject(NonNullableFormBuilder).group({
    search: [''],
    role: ['' as UserRole | ''],
    status: ['' as 'active' | 'inactive' | ''],
  });
  protected readonly items = signal<AdminAccount[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.requests
      .pipe(
        switchMap(params =>
          this.api.listUsers(params).pipe(
            catchError((error: HttpErrorResponse) => {
              this.error.set(describeHttpError(error, this.i18n.t('admin.users.loadError'), this.i18n.t));
              this.loading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(result => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.page.set(result.page);
        this.pageSize.set(result.pageSize);
        this.loading.set(false);
      });

    this.filters.valueChanges.pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load(1));
    this.load(1);
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.load(event.pageIndex + 1);
  }

  protected reload(): void {
    this.load(this.page());
  }

  private load(page: number): void {
    const f = this.filters.getRawValue();
    this.loading.set(true);
    this.error.set(null);
    this.requests.next({ page, pageSize: this.pageSize(), search: f.search, role: f.role, status: f.status });
  }
}
