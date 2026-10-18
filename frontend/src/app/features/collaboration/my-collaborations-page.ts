import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { priorityTone, statusLabel, statusTone } from '../tickets/ticket-models';
import { CollaborationApi } from './collaboration-api';
import { MyCollaboration } from './collaboration-models';

@Component({
  selector: 'crm-my-collaborations-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, RouterLink, PageHeader, StatusBadge, MatButtonModule, MatPaginatorModule, MatProgressBarModule],
  templateUrl: './my-collaborations-page.html',
  styleUrl: '../../shared/ui/productivity-page.scss',
})
export class MyCollaborationsPage {
  private readonly api = inject(CollaborationApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<number>();

  protected readonly priorityTone = priorityTone;
  protected readonly statusLabel = statusLabel;
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
              this.error.set(describeHttpError(error, 'Could not load your collaborations.'));
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
