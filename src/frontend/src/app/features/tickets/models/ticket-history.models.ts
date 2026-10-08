import { CreationSnapshot, ReferenceChoice, TicketPriority } from './ticket.models';
interface HistoryEntry {
  id: string; revision: string; sequence: number;
  actor: { issuer: string; subject: string; displayName: string }; occurredAtUtc: string;
}
export type TicketHistory = HistoryEntry & (
  { kind: 'Created'; field: null; previousValue: null; newValue: CreationSnapshot } |
  { kind: 'DetailsChanged'; field: 'subject' | 'description'; previousValue: string; newValue: string } |
  { kind: 'DetailsChanged'; field: 'priority'; previousValue: TicketPriority; newValue: TicketPriority } |
  { kind: 'DetailsChanged'; field: 'category'; previousValue: ReferenceChoice; newValue: ReferenceChoice } |
  { kind: 'AssignmentChanged'; field: 'assignedAgent'; previousValue: ReferenceChoice | null; newValue: ReferenceChoice | null }
);
