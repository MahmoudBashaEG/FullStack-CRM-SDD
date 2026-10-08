import { TicketStatus } from './ticket.models';
export const ticketStatusLabels: Record<TicketStatus, string> = { Open: 'Open', InProgress: 'In Progress', Resolved: 'Resolved', Closed: 'Closed' };
const nextStatuses: Record<TicketStatus, readonly TicketStatus[]> = {
  Open: ['InProgress', 'Resolved'], InProgress: ['Resolved'], Resolved: ['Closed', 'Open'], Closed: ['Open']
};
export function allowedNextStatuses(status: TicketStatus): readonly TicketStatus[] { return nextStatuses[status] ?? []; }
