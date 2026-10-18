import { Translate, englishT } from '../../core/i18n/translate-fn';
import { PortalTimelineEntry } from './portal-models';

export interface TimelineText { title: string; text: string; }

/**
 * Customer wording for one timeline entry. Facts only: no actor, no reason, no internal detail (CP3).
 * Creation wording is neutral: staff may have created the ticket on the customer's behalf.
 */
export function describeTimelineEntry(entry: PortalTimelineEntry, t: Translate = englishT): TimelineText {
  const words = (key: string): TimelineText => ({ title: t(`portal.timeline.${key}.title`), text: t(`portal.timeline.${key}.text`) });
  if (entry.kind === 'Created') {
    return words(entry.isRecorded ? 'created' : 'createdLegacy');
  }
  if (entry.fromStatus === 'Closed' && entry.status === 'Open') {
    return words('reopened');
  }
  if (entry.fromStatus === 'Resolved' && entry.status === 'InProgress') {
    return words('backInProgress');
  }
  switch (entry.status) {
    case 'InProgress':
      return words('inProgress');
    case 'Resolved':
      return words('resolved');
    case 'Closed':
      return words('closed');
    default:
      return words('open');
  }
}
