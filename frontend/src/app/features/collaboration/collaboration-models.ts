import { TicketPriority, TicketStatus } from '../tickets/ticket-models';

export type CollaborationAccess = 'SuperAdmin' | 'PrimaryAgent' | 'Collaborator';

/** Matches Crm.Api CollaborationTicketResponse: deliberately no description, customer or activity data. */
export interface CollaborationTicket {
  ticketId: string;
  subject: string;
  status: TicketStatus;
  priority: TicketPriority;
  assignedAgentName: string | null;
  access: CollaborationAccess;
  canManage: boolean;
}

export interface TicketMessage {
  id: number;
  authorUserId: string;
  authorName: string;
  body: string;
  createdAtUtc: string;
}

export interface TicketCollaborator {
  agentUserId: string;
  agentName: string;
  isActive: boolean;
  addedByName: string;
  addedAtUtc: string;
}

export interface CollaboratorCandidate {
  agentUserId: string;
  agentName: string;
}

export interface MyCollaboration {
  ticketId: string;
  subject: string;
  status: TicketStatus;
  priority: TicketPriority;
  isSuspended: boolean;
  isPrimaryAssignee: boolean;
  addedAtUtc: string;
  lastMessageAtUtc: string | null;
}
