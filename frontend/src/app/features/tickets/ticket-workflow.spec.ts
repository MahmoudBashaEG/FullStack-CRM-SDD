import { ticketActions } from './ticket-workflow';

describe('ticketActions', () => {
  it('lets the assigned Agent resolve and escalate an In Progress ticket, but not reassign or clear escalation', () => {
    const actions = ticketActions(
      { status: 'InProgress', isEscalated: false, assignedAgentUserId: 'agent-1' },
      { id: 'agent-1', role: 'Agent' },
    );

    expect(actions.transitions).toEqual([{ to: 'Resolved', label: 'Resolve' }]);
    expect(actions.canEscalate).toBe(true);
    expect(actions.canAssign).toBe(false);
    expect(actions.canClearEscalation).toBe(false);
  });
});
