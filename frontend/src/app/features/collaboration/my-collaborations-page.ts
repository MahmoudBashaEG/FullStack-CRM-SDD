import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { ErrorState } from '../../shared/ui/error-state/error-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { priorityTone, statusTone } from '../tickets/ticket-models';
import { CollaborationApi } from './collaboration-api';
import { MyCollaboration } from './collaboration-models';

@Component({
  selector: 'crm-my-collaborations-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizedDatePipe, TranslatePipe, RouterLink, EmptyState, ErrorState, PageHeader, StatusBadge, MatCardModule, MatPaginatorModule, MatProgressBarModule],
  templateUrl: './my-collaborations-page.html',
  styleUrl: '../../shared/ui/productivity-page.scss',
})
export class MyCollaborationsPage {
  private readonly api = inject(CollaborationApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly i18n = inject(I18n);
  private readonly requests = new Subject<number>();

  protected readonly priorityTone = priorityTone;
  protected readonly statusTone = statusTone;
  protected readonly items = signal<MyCollaboration[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.requests
      .pipe(
        switchMap(page =>
          this.api.mine(page, this.pageSize()).pipe(
            catchError((error: HttpErrorResponse) => {
              this.error.set(describeHttpError(error, this.i18n.t('collaboration.mine.loadError'), this.i18n.t));
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
        this.loading.set(false);
      });
    this.load(1);
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.load(event.pageIndex + 1);
  }

  protected load(page: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.requests.next(page);
  }
}
