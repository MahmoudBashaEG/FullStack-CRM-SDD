export type TicketPriority = 'Low' | 'Normal' | 'High' | 'Urgent';
export type TicketStatus = 'Open' | 'InProgress' | 'Resolved' | 'Closed';
export interface ReferenceChoice { id: string; displayName: string; }
export interface TicketCategory extends ReferenceChoice { isAvailable: boolean; }
export interface TicketAgent extends ReferenceChoice { isEligible: boolean; }
export interface CreateTicket { subject: string; description: string; categoryId?: string; priority?: TicketPriority; }
export interface TicketDetail {
  reference: string; subject: string; description: string; category: TicketCategory;
  priority: TicketPriority; status: TicketStatus; assignedAgent: TicketAgent | null;
  createdAtUtc: string; updatedAtUtc: string; editVersion: string;
}
export interface CreationSnapshot {
  reference: string; subject: string; description: string; category: ReferenceChoice;
  priority: TicketPriority; status: TicketStatus; assignedAgent: null;
}
export interface TicketHistory {
  id: string; revision: string; sequence: number; kind: 'Created'; field: null;
  previousValue: null; newValue: CreationSnapshot;
  actor: { issuer: string; subject: string; displayName: string }; occurredAtUtc: string;
}
export interface Page<T> { items: T[]; page: number; pageSize: number; hasMore: boolean; }
export const FALLBACK_CATEGORY = '00000000-0000-0000-0000-000000000001';
