import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { I18n } from '../../core/i18n/i18n';
import { LocalizedDatePipe } from '../../core/i18n/localized-date.pipe';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { SlaIndicator, SlaKind, SlaTargetView, slaIndicatorTone } from '../sla/sla-models';
import { Ticket } from './ticket-models';

/** Backend-computed SLA state of one ticket (SLA-001). Display only; Mark as Responded asks the parent to call the API. */
@Component({
  selector: 'crm-ticket-sla-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizedDatePipe, TranslatePipe, StatusBadge, MatButtonModule],
  templateUrl: './ticket-sla-card.html',
  styleUrl: './ticket-sla-card.scss',
})
export class TicketSlaCard {
  readonly ticket = input.required<Ticket>();
  readonly canMarkResponded = input(false);
  readonly busy = input(false);
  readonly markResponded = output<void>();

  private readonly i18n = inject(I18n);
  protected readonly indicatorTone = slaIndicatorTone;

  protected indicatorLabel(indicator: SlaIndicator): string {
    return this.i18n.t(`tickets.sla.indicator.${indicator}`);
  }

  protected kindLabel(kind: SlaKind): string {
    return this.i18n.t(`tickets.sla.kind.${kind}`);
  }

  /** 135 -> '2 h 15 min' (translated units). Callers handle the overdue sign. */
  protected format(minutes: number): string {
    const total = Math.max(0, Math.round(minutes));
    const hours = Math.floor(total / 60);
    const rest = total % 60;
    if (hours === 0) {
      return this.i18n.t('tickets.sla.minutes', { n: rest });
    }
    return rest === 0 ? this.i18n.t('tickets.sla.hours', { h: hours }) : this.i18n.t('tickets.sla.hoursMinutes', { h: hours, m: rest });
  }

  protected targets(): SlaTargetView[] {
    const sla = this.ticket().sla;
    return sla ? [sla.firstResponse, sla.resolution] : [];
  }

  protected completedLabel(target: SlaTargetView): string {
    return this.i18n.t(target.kind === 'FirstResponse' ? 'tickets.sla.responded' : 'tickets.sla.resolved');
  }
}
