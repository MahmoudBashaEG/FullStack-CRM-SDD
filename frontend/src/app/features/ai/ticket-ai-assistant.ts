import { Clipboard } from '@angular/cdk/clipboard';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnChanges,
  OnInit,
  SimpleChanges,
  WritableSignal,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';
import { Observable, Subject, takeUntil } from 'rxjs';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { copyQuickReply } from '../quick-replies/copy-quick-reply';
import { Ticket } from '../tickets/ticket-models';
import { TicketsApi } from '../tickets/tickets-api';
import { AiApi } from './ai-api';
import { describeAiError } from './ai-errors';
import {
  AI_REPLY_INSTRUCTION_MAX_LENGTH,
  AI_REPLY_TONES,
  AiCategorySuggestion,
  AiReplyTone,
  AiSolutions,
  AiText,
} from './ai-models';

/**
 * AI-001 card on the authorized Ticket Details page. Every result is advisory plain text; nothing is saved or sent automatically.
 * The only write is the explicit "Apply suggestion" (PUT /tickets/{id}/category, D1).
 */
@Component({
  selector: 'crm-ticket-ai-assistant',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    LocalizedDatePipe,
    StatusBadge,
    MatTabsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressBarModule,
  ],
  templateUrl: './ticket-ai-assistant.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './ticket-ai-assistant.scss'],
})
export class TicketAiAssistant implements OnInit, OnChanges {
  private readonly ai = inject(AiApi);
  private readonly ticketsApi = inject(TicketsApi);
  private readonly clipboard = inject(Clipboard);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly visitEnded = new Subject<void>();
  private visitTicketId: string | null = null;

  readonly ticket = input.required<Ticket>();
  readonly ticketUpdated = output<Ticket>();
  readonly accessLost = output<void>();

  protected readonly tones = AI_REPLY_TONES;
  protected readonly i18n = inject(I18n);
  protected readonly instructionMax = AI_REPLY_INSTRUCTION_MAX_LENGTH;

  protected readonly suggestion = signal<AiCategorySuggestion | null>(null);
  protected readonly suggestionBusy = signal(false);
  protected readonly suggestionError = signal<string | null>(null);
  protected readonly applied = signal<string | null>(null);

  protected readonly summary = signal<AiText | null>(null);
  protected readonly summaryBusy = signal(false);
  protected readonly summaryError = signal<string | null>(null);

  protected readonly tone = new FormControl<AiReplyTone | null>(null);
  protected readonly instructions = new FormControl('', { nonNullable: true });
  protected readonly draft = new FormControl('', { nonNullable: true });
  protected readonly draftResult = signal<AiText | null>(null);
  protected readonly draftBusy = signal(false);
  protected readonly draftError = signal<string | null>(null);

  protected readonly solutions = signal<AiSolutions | null>(null);
  protected readonly solutionsBusy = signal(false);
  protected readonly solutionsError = signal<string | null>(null);

  // The required input is only read from ngOnInit/ngOnChanges, never from the constructor. No hook ever calls the AI:
  // every AI request (including the category suggestion) starts from an explicit user click.
  ngOnInit(): void {
    this.startVisit(this.ticket().id);
  }

  /** A different ticket id starts a new visit (state reset only, no request). The same id (Apply, 409 refresh, Edit-card save) keeps all results and typed text. */
  ngOnChanges(changes: SimpleChanges): void {
    const change = changes['ticket'];
    if (change && !change.firstChange && (change.currentValue as Ticket).id !== this.visitTicketId) {
      this.startVisit((change.currentValue as Ticket).id);
    }
  }

  private startVisit(id: string): void {
    this.visitEnded.next(); // cancels in-flight AI/Apply requests of the previous ticket
    this.suggestion.set(null);
    this.applied.set(null);
    this.summary.set(null);
    this.solutions.set(null);
    this.draftResult.set(null);
    this.draft.reset('');
    this.tone.reset(null);
    this.instructions.reset('');
    for (const flag of [this.suggestionBusy, this.summaryBusy, this.draftBusy, this.solutionsBusy]) {
      flag.set(false);
    }
    for (const error of [this.suggestionError, this.summaryError, this.draftError, this.solutionsError]) {
      error.set(null);
    }
    this.visitTicketId = id;
  }

  protected suggestCategory(): void {
    const id = this.ticket().id;
    this.applied.set(null);
    this.run(this.ai.categorySuggestion(id), id, this.suggestionBusy, this.suggestionError, this.i18n.t('ai.ticket.categoryFailed'), s => {
      this.suggestion.set(s);
    });
  }

  protected generateSummary(): void {
    const id = this.ticket().id;
    this.run(this.ai.summary(id), id, this.summaryBusy, this.summaryError, this.i18n.t('ai.ticket.summaryFailed'), s => this.summary.set(s));
  }

  protected generateDraft(): void {
    const tone = this.tone.value;
    if (!tone) {
      return;
    }
    const id = this.ticket().id;
    this.run(this.ai.replyDraft(id, tone, this.instructions.value), id, this.draftBusy, this.draftError, this.i18n.t('ai.ticket.draftFailed'), r => {
      this.draftResult.set(r);
      this.draft.setValue(r.text);
    });
  }

  protected discardDraft(): void {
    this.draftResult.set(null);
    this.draft.reset('');
  }

  protected generateSolutions(): void {
    const id = this.ticket().id;
    this.run(this.ai.solutions(id), id, this.solutionsBusy, this.solutionsError, this.i18n.t('ai.ticket.solutionsFailed'), s => this.solutions.set(s));
  }

  protected dismiss(): void {
    this.suggestion.set(null); // local only, nothing is sent
  }

  protected apply(suggestion: AiCategorySuggestion): void {
    const current = this.ticket();
    this.suggestionBusy.set(true);
    this.suggestionError.set(null);
    this.ticketsApi
      .changeCategory(current.id, { expectedCategory: current.category, category: suggestion.suggestedCategory })
      .pipe(takeUntil(this.visitEnded), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: updated => {
          this.suggestionBusy.set(false);
          this.suggestion.set(null);
          this.applied.set(this.i18n.t('ai.ticket.applied', { category: this.i18n.t(`enums.ticketCategory.${updated.category}`) }));
          this.snackBar.open(this.i18n.t('ai.ticket.categoryUpdated'), this.i18n.t('quickReplies.ok'), { duration: 4000 });
          this.ticketUpdated.emit(updated);
        },
        error: (error: HttpErrorResponse) => {
          this.suggestionBusy.set(false);
          if (error.status === 404) {
            this.accessLost.emit();
            return;
          }
          this.suggestionError.set(describeAiError(error, this.i18n.t('ai.ticket.applyFailed'), this.i18n.t));
          if (error.status === 409) {
            this.refreshTicket(current.id);
          }
        },
      });
  }

  protected copy(text: string): void {
    copyQuickReply(this.clipboard, this.snackBar, text, this.i18n.t);
  }

  protected copyAllSolutions(s: AiSolutions): void {
    const lines: string[] = [];
    for (const r of s.knowledgeBaseRecommendations) {
      lines.push(r.articleTitle, ...r.steps.map(step => `- ${step}`));
    }
    if (s.generalRecommendations.length > 0) {
      lines.push(this.i18n.t('ai.ticket.copyGeneralHeading'), ...s.generalRecommendations.map(g => `- ${g}`));
    }
    this.copy(lines.join('\n'));
  }

  private refreshTicket(id: string): void {
    this.ticketsApi
      .get(id)
      .pipe(takeUntil(this.visitEnded), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: fresh => this.ticketUpdated.emit(fresh),
        error: (error: HttpErrorResponse) => {
          if (error.status === 404) {
            this.accessLost.emit();
          }
        },
      });
  }

  /** Shared runner: busy guard, 404 = access lost, otherwise the message; existing results and typed text are kept. */
  private run<T>(
    call: Observable<T>,
    ticketId: string,
    busy: WritableSignal<boolean>,
    error: WritableSignal<string | null>,
    fallback: string,
    onResult: (result: T) => void,
  ): void {
    busy.set(true);
    error.set(null);
    call.pipe(takeUntil(this.visitEnded), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => {
        busy.set(false);
        if (ticketId === this.visitTicketId) {
          onResult(result);
        }
      },
      error: (e: HttpErrorResponse) => {
        busy.set(false);
        if (e.status === 404) {
          this.accessLost.emit();
          return;
        }
        error.set(describeAiError(e, fallback, this.i18n.t));
      },
    });
  }
}
