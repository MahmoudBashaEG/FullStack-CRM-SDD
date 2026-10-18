import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { SlaTargetView, formatBusinessMinutes, slaIndicatorLabel, slaIndicatorTone, slaKindLabel } from '../sla/sla-models';
import { Ticket } from './ticket-models';

/** Backend-computed SLA state of one ticket (SLA-001). Display only; Mark as Responded asks the parent to call the API. */
@Component({
  selector: 'crm-ticket-sla-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, StatusBadge, MatButtonModule],
  templateUrl: './ticket-sla-card.html',
  styleUrl: './ticket-sla-card.scss',
})
export class TicketSlaCard {
  readonly ticket = input.required<Ticket>();
  readonly canMarkResponded = input(false);
  readonly busy = input(false);
  readonly markResponded = output<void>();

  protected readonly indicatorLabel = slaIndicatorLabel;
  protected readonly indicatorTone = slaIndicatorTone;
  protected readonly format = formatBusinessMinutes;
  protected readonly kindLabel = slaKindLabel;

  protected targets(): SlaTargetView[] {
    const sla = this.ticket().sla;
    return sla ? [sla.firstResponse, sla.resolution] : [];
  }

  protected completedLabel(target: SlaTargetView): string {
    return target.kind === 'FirstResponse' ? 'Responded' : 'Resolved';
  }
}
