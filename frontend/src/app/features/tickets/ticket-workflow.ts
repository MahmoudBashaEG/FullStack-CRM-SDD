import { CurrentUser } from '../../core/auth/auth-models';
import { Ticket, TicketStatus } from './ticket-models';

export interface TicketTransition {
  to: TicketStatus;
  label: string;
}

/** Must match Crm.Domain.Tickets.TicketWorkflow. Display only — the server re-checks everything. */
export const TICKET_TRANSITIONS: Readonly<Record<TicketStatus, readonly TicketTransition[]>> = {
  Open: [{ to: 'InProgress', label: 'Start progress' }],
  InProgress: [{ to: 'Resolved', label: 'Resolve' }],
  Resolved: [
    { to: 'Closed', label: 'Close' },
    { to: 'InProgress', label: 'Resume' },
  ],
  Closed: [{ to: 'Open', label: 'Reopen' }],
};

export interface TicketActions {
  transitions: readonly TicketTransition[];
  canAssign: boolean;
  canEscalate: boolean;
  canClearEscalation: boolean;
}

/** SuperAdmin: everything. Agent: workflow + escalate only on tickets currently assigned to them. Others: nothing. */
export function ticketActions(
  ticket: Pick<Ticket, 'status' | 'isEscalated' | 'assignedAgentUserId'>,
  user: Pick<CurrentUser, 'id' | 'role'> | null,
): TicketActions {
  const isSuperAdmin = user?.role === 'SuperAdmin';
  const canAct = isSuperAdmin || (user?.role === 'Agent' && ticket.assignedAgentUserId === user.id);
  const active = ticket.status === 'Open' || ticket.status === 'InProgress';
  return {
    transitions: canAct ? TICKET_TRANSITIONS[ticket.status] : [],
    canAssign: isSuperAdmin,
    canEscalate: canAct && active && !ticket.isEscalated,
    canClearEscalation: isSuperAdmin && ticket.isEscalated,
  };
}
