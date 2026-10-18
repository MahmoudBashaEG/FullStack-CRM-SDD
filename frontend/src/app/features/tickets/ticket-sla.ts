import { CurrentUser } from '../../core/auth/auth-models';
import { Ticket } from './ticket-models';

/** Display only — the server re-checks. SuperAdmin or the assigned Agent, tracked, not yet recorded, Open/InProgress. */
export function canMarkFirstResponse(
  ticket: Pick<Ticket, 'status' | 'assignedAgentUserId' | 'sla'>,
  user: Pick<CurrentUser, 'id' | 'role'> | null,
): boolean {
  if (!user || !ticket.sla || ticket.sla.firstResponse.completedAtUtc !== null) {
    return false;
  }
  if (ticket.status !== 'Open' && ticket.status !== 'InProgress') {
    return false;
  }
  return user.role === 'SuperAdmin' || (user.role === 'Agent' && ticket.assignedAgentUserId === user.id);
}
