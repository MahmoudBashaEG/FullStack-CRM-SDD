import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { EMPTY, Subject, catchError, debounceTime, switchMap } from 'rxjs';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { describeSaveError } from '../sla/sla-errors';
import { AdminApi } from './admin-api';
import { auditActionLabel, describeAuditChange, formatUtcTimestamp } from './admin-format';
import { AUDIT_ACTIONS, AuditAction, AuditListParams, AuditLogEntry } from './admin-models';
import { AdminSectionNav } from './admin-section-nav';

@Component({
  selector: 'crm-audit-logs-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    PageHeader,
    AdminSectionNav,
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
  private readonly api = inject(AdminApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<AuditListParams>();

  protected readonly actions = AUDIT_ACTIONS;
  protected readonly auditActionLabel = auditActionLabel;
  protected readonly describeAuditChange = describeAuditChange;
  protected readonly formatUtcTimestamp = formatUtcTimestamp;
  protected readonly pageSizeOptions = [20, 50, 100];
  protected readonly filters = inject(NonNullableFormBuilder).group({
    action: ['' as AuditAction | ''],
    from: [''],
    to: [''],
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
              this.error.set(describeSaveError(error, 'Could not load the audit log.'));
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
    if (f.from && f.to && f.to < f.from) {
      this.rangeError.set("'To' must be on or after 'From'.");
      return; // no request; the server 400 stays authoritative
    }

    this.rangeError.set(null);
    this.loading.set(true);
    this.error.set(null);
    this.requests.next({ page, pageSize: this.pageSize(), action: f.action, from: f.from, to: f.to, search: f.search });
  }
}
