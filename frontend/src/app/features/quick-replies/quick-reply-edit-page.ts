import { Clipboard } from '@angular/cdk/clipboard';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/describe-http-error';
import { AuthService } from '../../core/auth/auth-service';
import { I18n } from '../../core/i18n/i18n';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { TICKET_CATEGORIES, TicketCategory } from '../tickets/ticket-models';
import { copyQuickReply } from './copy-quick-reply';
import { QuickReply } from './quick-reply-models';
import { QuickRepliesApi } from './quick-replies-api';

@Component({
  selector: 'crm-quick-reply-edit-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    TranslatePipe,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
  ],
  templateUrl: './quick-reply-edit-page.html',
  styleUrl: '../../shared/ui/productivity-page.scss',
})
export class QuickReplyEditPage {
  private readonly i18n = inject(I18n);
  private readonly api = inject(QuickRepliesApi);
  private readonly auth = inject(AuthService);
  private readonly clipboard = inject(Clipboard);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);

  protected readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id');
  protected readonly isNew = this.id === null;
  protected readonly categories = TICKET_CATEGORIES;
  protected readonly item = signal<QuickReply | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly form = inject(NonNullableFormBuilder).group({
    title: ['', [Validators.required, Validators.maxLength(120)]],
    category: ['General' as TicketCategory, Validators.required],
    body: ['', [Validators.required, Validators.maxLength(2000)]],
  });

  constructor() {
    if (this.id) {
      this.api.get(this.id).subscribe({
        next: q => this.apply(q),
        error: (error: HttpErrorResponse) =>
          this.loadError.set(error.status === 404 ? this.i18n.t('quickReplies.edit.unavailable') : describeHttpError(error, this.i18n.t('quickReplies.edit.loadFailed'), this.i18n.t)),
      });
    }
  }

  protected scopeLine(): string {
    const scope = this.item()?.scope ?? (this.auth.role() === 'SuperAdmin' ? 'Shared' : 'Personal');
    return this.i18n.t(scope === 'Shared' ? 'quickReplies.edit.scopeShared' : 'quickReplies.edit.scopePersonal');
  }

  protected save(): void {
    if (this.form.invalid || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const body = { title: v.title.trim(), body: v.body.trim(), category: v.category };
    this.run(this.id ? this.api.update(this.id, body) : this.api.create(body), q => {
      this.snackBar.open(this.i18n.t('quickReplies.edit.saved'), this.i18n.t('quickReplies.ok'), { duration: 4000 });
      if (this.isNew) {
        void this.router.navigate(['/quick-replies', q.id]);
      } else {
        this.apply(q);
      }
    });
  }

  protected setArchived(archived: boolean): void {
    const id = this.id;
    if (id) this.run(archived ? this.api.archive(id) : this.api.restore(id), q => this.apply(q));
  }

  protected copy(): void {
    copyQuickReply(this.clipboard, this.snackBar, this.item()?.body ?? this.form.controls.body.value, this.i18n.t);
  }

  private run(request: ReturnType<QuickRepliesApi['get']>, done: (q: QuickReply) => void): void {
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: q => {
        this.busy.set(false);
        done(q);
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(describeHttpError(error, this.i18n.t('quickReplies.edit.saveFailed'), this.i18n.t));
      },
    });
  }

  private apply(q: QuickReply): void {
    this.item.set(q);
    this.form.reset({ title: q.title, category: q.category, body: q.body });
    if (!q.canEdit || q.isArchived) {
      this.form.disable();
    } else {
      this.form.enable();
    }
  }
}
