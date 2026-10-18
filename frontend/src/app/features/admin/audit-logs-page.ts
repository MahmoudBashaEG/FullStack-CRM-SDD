import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { EMPTY, Subject, catchError, debounceTime, switchMap } from 'rxjs';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { toDateOnly } from '../../shared/date/date-only';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { describeSaveError } from '../sla/sla-errors';
import { AdminApi } from './admin-api';
import { auditActionLabel, describeAuditChange } from './admin-format';
import { AUDIT_ACTIONS, AuditAction, AuditListParams, AuditLogEntry } from './admin-models';
import { AdminSectionNav } from './admin-section-nav';

@Component({
  selector: 'crm-audit-logs-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    PageHeader,
    AdminSectionNav,
    EmptyState,
    ErrorState,
    TranslatePipe,
    LocalizedDatePipe,
    MatCardModule,
    MatDatepickerModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './audit-logs-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './admin-page.scss'],
})
export class AuditLogsPage {
  protected readonly i18n = inject(I18n);
  private readonly api = inject(AdminApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<AuditListParams>();

  protected readonly actions = AUDIT_ACTIONS;
  protected readonly auditActionLabel = auditActionLabel;
  protected readonly describeAuditChange = describeAuditChange;
  protected readonly pageSizeOptions = [20, 50, 100];
  protected readonly filters = inject(NonNullableFormBuilder).group({
    action: ['' as AuditAction | ''],
    from: [null as Date | null],
    to: [null as Date | null],
    search: [''],
  });
  protected readonly items = signal<AuditLogEntry[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly rangeError = signal<string | null>(null);

  constructor() {
    this.requests
      .pipe(
        switchMap(params =>
          this.api.listAuditLogs(params).pipe(
            catchError((error: HttpErrorResponse) => {
              this.error.set(describeSaveError(error, this.i18n.t('admin.auditPage.loadError'), this.i18n.t));
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
    const from = toDateOnly(f.from) ?? '';
    const to = toDateOnly(f.to) ?? '';
    if (from && to && to < from) {
      this.rangeError.set(this.i18n.t('admin.auditPage.rangeError'));
      return; // no request; the server 400 stays authoritative
    }

    this.rangeError.set(null);
    this.loading.set(true);
    this.error.set(null);
    this.requests.next({ page, pageSize: this.pageSize(), action: f.action, from, to, search: f.search });
  }
}
