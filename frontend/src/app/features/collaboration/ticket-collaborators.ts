import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { CollaborationApi } from './collaboration-api';
import { CollaboratorCandidate, TicketCollaborator } from './collaboration-models';

/** Active collaborators of one ticket. Managers (SuperAdmin / the ticket's assigned Agent) can invite and remove. */
@Component({
  selector: 'crm-ticket-collaborators',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatSelectModule],
  templateUrl: './ticket-collaborators.html',
  styleUrl: '../../shared/ui/productivity-page.scss',
})
export class TicketCollaborators {
  private readonly api = inject(CollaborationApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  readonly ticketId = input.required<string>();
  readonly canManage = input(false);

  protected readonly collaborators = signal<TicketCollaborator[]>([]);
  protected readonly candidates = signal<CollaboratorCandidate[]>([]);
  protected readonly selection = new FormControl<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);

  constructor() {
    effect(() => {
      const id = this.ticketId();
      const manage = this.canManage();
      untracked(() => this.load(id, manage));
    });
  }

  protected invite(): void {
    const agentId = this.selection.value;
    if (!agentId || this.busy()) return;
    this.run(this.api.invite(this.ticketId(), agentId), 'Collaborator invited.');
  }

  protected remove(c: TicketCollaborator): void {
    if (this.busy() || !window.confirm(`Remove ${c.agentName} from this discussion?`)) return;
    this.run(this.api.remove(this.ticketId(), c.agentUserId), 'Collaborator removed.');
  }

  private run(request: Observable<unknown>, success: string): void {
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: () => {
        this.busy.set(false);
        this.selection.reset(null);
        this.snackBar.open(success, 'OK', { duration: 3000 });
        this.load(this.ticketId(), this.canManage());
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        if (error.status === 503) {
          this.error.set('Your change may have been saved. Check the list before trying again.');
        } else {
          this.error.set(describeHttpError(error, 'Could not change the collaborators.'));
        }
        this.load(this.ticketId(), this.canManage());
      },
    });
  }

  private load(id: string, manage: boolean): void {
    this.api.collaborators(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: list => this.collaborators.set(list),
      error: (error: HttpErrorResponse) => this.error.set(describeHttpError(error, 'Could not load the collaborators.')),
    });
    if (manage) {
      this.api.candidates(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: list => this.candidates.set(list),
        error: () => this.candidates.set([]),
      });
    }
  }
}
