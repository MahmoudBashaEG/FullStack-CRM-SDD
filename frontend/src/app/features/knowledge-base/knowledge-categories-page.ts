import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/describe-http-error';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { describeSaveError } from '../sla/sla-errors';
import { KnowledgeBaseApi } from './knowledge-base-api';
import { KB_LIMITS, KnowledgeCategory } from './knowledge-base-models';

@Component({
  selector: 'crm-knowledge-categories-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, PageHeader, StatusBadge, MatFormFieldModule, MatInputModule, MatButtonModule],
  templateUrl: './knowledge-categories-page.html',
  styleUrl: '../../shared/ui/productivity-page.scss',
})
export class KnowledgeCategoriesPage {
  private readonly api = inject(KnowledgeBaseApi);

  protected readonly maxLength = KB_LIMITS.categoryName;
  protected readonly categories = signal<KnowledgeCategory[]>([]);
  protected readonly names = new Map<string, FormControl<string>>();
  protected readonly loadError = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly addForm = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(KB_LIMITS.categoryName)]],
  });

  constructor() {
    this.load();
  }

  protected nameFor(id: string): FormControl<string> {
    return this.names.get(id)!;
  }

  protected add(): void {
    if (this.addForm.invalid || this.busy()) {
      this.addForm.markAllAsTouched();
      return;
    }
    this.run(this.api.createCategory(this.addForm.controls.name.value.trim()), () => this.addForm.reset({ name: '' }));
  }

  protected save(category: KnowledgeCategory, isActive: boolean): void {
    const control = this.nameFor(category.id);
    if (control.invalid || this.busy()) {
      return;
    }
    this.run(this.api.updateCategory(category.id, { name: control.value.trim(), isActive }));
  }

  protected load(): void {
    this.loadError.set(null);
    this.api.listCategories(true).subscribe({
      next: categories => {
        this.names.clear();
        for (const c of categories) {
          this.names.set(
            c.id,
            new FormControl(c.name, { nonNullable: true, validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(KB_LIMITS.categoryName)] }),
          );
        }
        this.categories.set(categories);
      },
      error: (error: HttpErrorResponse) => this.loadError.set(describeHttpError(error, 'Could not load categories.')),
    });
  }

  private run(request: ReturnType<KnowledgeBaseApi['createCategory']>, done?: () => void): void {
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: () => {
        this.busy.set(false);
        done?.();
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(describeSaveError(error, 'Could not save the category.'));
      },
    });
  }
}
