import { PortalTimelineEntry } from './portal-models';

export interface TimelineText { title: string; text: string; }

/**
 * Customer wording for one timeline entry. Facts only: no actor, no reason, no internal detail (CP3).
 * Creation wording is neutral: staff may have created the ticket on the customer's behalf.
 */
export function describeTimelineEntry(entry: PortalTimelineEntry): TimelineText {
  if (entry.kind === 'Created') {
    return entry.isRecorded
      ? { title: 'Request created', text: 'A support request was created for you.' }
      : { title: 'Request created', text: 'Earlier status history is not available for this request.' };
  }
  if (entry.fromStatus === 'Closed' && entry.status === 'Open') {
    return { title: 'Reopened', text: 'Your request was reopened.' };
  }
  if (entry.fromStatus === 'Resolved' && entry.status === 'InProgress') {
    return { title: 'Back in progress', text: 'We are continuing to work on your request.' };
  }
  switch (entry.status) {
    case 'InProgress':
      return { title: 'In progress', text: 'Our team is working on your request.' };
    case 'Resolved':
      return { title: 'Resolved', text: 'Your request has been resolved.' };
    case 'Closed':
      return { title: 'Closed', text: 'Your request is closed.' };
    default:
      return { title: 'Open', text: 'Your request is open.' };
  }
}
