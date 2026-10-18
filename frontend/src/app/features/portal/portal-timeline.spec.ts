import { PortalTimelineEntry } from './portal-models';
import { describeTimelineEntry } from './portal-timeline';

const at = '2030-01-01T00:00:00Z';
const entry = (e: Partial<PortalTimelineEntry>): PortalTimelineEntry =>
  ({ kind: 'StatusChanged', fromStatus: null, status: 'Open', occurredAtUtc: at, isRecorded: true, ...e });

describe('describeTimelineEntry', () => {
  it('words neutral creation, legacy creation, progress, resolution, closing, resuming and reopening without internal detail', () => {
    const created = describeTimelineEntry(entry({ kind: 'Created' }));
    const legacy = describeTimelineEntry(entry({ kind: 'Created', isRecorded: false }));
    expect(created.title).toBe('Request created');
    expect(created.text).not.toMatch(/submitted|you (made|sent)/i); // staff may have created the ticket
    expect(legacy.title).toBe('Request created');
    expect(legacy.text).toContain('not available');
    expect(describeTimelineEntry(entry({ fromStatus: 'Open', status: 'InProgress' })).title).toBe('In progress');
    expect(describeTimelineEntry(entry({ fromStatus: 'InProgress', status: 'Resolved' })).title).toBe('Resolved');
    expect(describeTimelineEntry(entry({ fromStatus: 'Resolved', status: 'Closed' })).title).toBe('Closed');
    expect(describeTimelineEntry(entry({ fromStatus: 'Resolved', status: 'InProgress' })).title).toBe('Back in progress');
    expect(describeTimelineEntry(entry({ fromStatus: 'Closed', status: 'Open' })).title).toBe('Reopened');
  });
});
