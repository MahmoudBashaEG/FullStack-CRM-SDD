import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { EMPTY, Subject, catchError, map, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { CustomerRecordsApi } from './customer-records-api';
import {
  ATTACHMENT_ACCEPT,
  ATTACHMENT_MAX_BYTES,
  CustomerAttachment,
  describeFieldError,
  formatFileSize,
} from './customer-records-models';

const PAGE_SIZE = 10;

/** Staff-only customer files. Upload and authenticated download only: no delete, preview or public URL. */
@Component({
  selector: 'crm-customer-attachments',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizedDatePipe, TranslatePipe, EmptyState, MatButtonModule, MatProgressBarModule],
  templateUrl: './customer-attachments.html',
  styleUrl: './customer-records.scss',
})
export class CustomerAttachments {
  private readonly i18n = inject(I18n);
  private readonly api = inject(CustomerRecordsApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<{ id: string; page: number }>();
  private nextPage = 1;

  readonly customerId = input.required<string>();

  protected readonly accept = ATTACHMENT_ACCEPT;
  protected readonly formatFileSize = formatFileSize;
  protected readonly items = signal<CustomerAttachment[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(false);
  protected readonly loaded = signal(false);
  protected readonly uploading = signal(false);
  protected readonly downloadingId = signal<string | null>(null);
  protected readonly selected = signal<File | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly fileError = signal<string | null>(null);
  protected readonly hasMore = computed(() => this.items().length < this.totalCount());

  constructor() {
    this.requests
      .pipe(
        switchMap(r =>
          this.api.attachments(r.id, r.page, PAGE_SIZE).pipe(
            map(result => ({ r, result })),
            catchError((error: HttpErrorResponse) => {
              this.loading.set(false);
              this.error.set(describeHttpError(error, this.i18n.t('customers.attachments.loadFailed'), this.i18n.t));
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
          const known = new Set(this.items().map(a => a.id));
          this.items.update(current => [...current, ...result.items.filter(a => !known.has(a.id))]);
        }
        this.totalCount.set(result.totalCount);
        this.nextPage = r.page + 1;
        this.loaded.set(true);
        this.loading.set(false);
      });

    effect(() => {
      const id = this.customerId();
      untracked(() => this.load(id, 1));
    });
  }

  protected onPicked(picker: HTMLInputElement): void {
    const file = picker.files?.[0] ?? null;
    this.selected.set(file);
    this.fileError.set(null);
    if (!file) {
      return;
    }
    const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
    if (file.size === 0) {
      this.fileError.set(this.i18n.t('customers.attachments.emptyFile'));
    } else if (file.size > ATTACHMENT_MAX_BYTES) {
      this.fileError.set(this.i18n.t('customers.attachments.tooLarge'));
    } else if (!ATTACHMENT_ACCEPT.split(',').includes(extension)) {
      this.fileError.set(this.i18n.t('customers.attachments.badType'));
    }
  }

  protected upload(picker: HTMLInputElement): void {
    const file = this.selected();
    if (!file || this.fileError() || this.uploading()) {
      return;
    }
    this.uploading.set(true);
    this.api.uploadAttachment(this.customerId(), file).subscribe({
      next: attachment => {
        this.uploading.set(false);
        this.items.update(current => [attachment, ...current.filter(a => a.id !== attachment.id)]);
        this.totalCount.update(n => n + 1);
        this.selected.set(null);
        picker.value = '';
        this.snackBar.open(this.i18n.t('customers.attachments.uploaded'), 'OK', { duration: 3000 });
      },
      error: (error: HttpErrorResponse) => {
        this.uploading.set(false);
        this.fileError.set(describeFieldError(error, 'file', this.i18n.t('customers.attachments.uploadFailed'), this.i18n.t));
      },
    });
  }

  protected download(attachment: CustomerAttachment): void {
    this.downloadingId.set(attachment.id);
    this.api.downloadAttachment(this.customerId(), attachment.id).subscribe({
      next: blob => {
        this.downloadingId.set(null);
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = attachment.fileName;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => {
        this.downloadingId.set(null);
        this.snackBar.open(this.i18n.t('customers.attachments.downloadFailed'), 'OK', { duration: 4000 });
      },
    });
  }

  protected loadMore(): void {
    this.load(this.customerId(), this.nextPage);
  }

  protected retry(): void {
    this.load(this.customerId(), this.items().length === 0 ? 1 : this.nextPage);
  }

  private load(id: string, page: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.requests.next({ id, page });
  }
}
