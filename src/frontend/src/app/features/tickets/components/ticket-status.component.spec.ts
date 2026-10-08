import { TestBed } from '@angular/core/testing';
import { TicketStatusComponent } from './ticket-status.component';
import { TicketHistoryComponent } from './ticket-history.component';
import { sampleTicket } from '../testing/ticket-fixtures';
import { TicketStatus } from '../models/ticket.models';
import { allowedNextStatuses } from '../models/ticket-lifecycle';

describe('TicketStatusComponent', () => {
  async function ready(status: TicketStatus = 'Open') { TestBed.configureTestingModule({ imports: [TicketStatusComponent] }); const fixture = TestBed.createComponent(TicketStatusComponent); const ticket = { ...sampleTicket, status }; fixture.componentRef.setInput('ticket', ticket); fixture.componentRef.setInput('seed', ticket); fixture.detectChanges(); await fixture.whenStable(); return fixture; }
  const expected: Record<TicketStatus, TicketStatus[]> = { Open: ['InProgress', 'Resolved'], InProgress: ['Resolved'], Resolved: ['Closed', 'Open'], Closed: ['Open'] };
  for (const from of ['Open', 'InProgress', 'Resolved', 'Closed'] as const) it(`offers exactly the allowed different statuses from ${from}`, async () => {
    const fixture = await ready(from); const choices = Array.from(fixture.nativeElement.querySelectorAll('option'), (option: any) => option.value).filter(Boolean);
    expect(choices).toEqual(expected[from]); expect(allowedNextStatuses(from)).toEqual(expected[from]);
  });
  it('dispatches a deliberate DOM submit and leaves current ticket unchanged until server success', async () => {
    const fixture = await ready(); const save = vi.fn(); fixture.componentInstance.save.subscribe(save); fixture.componentInstance.status.setValue('Resolved');
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    expect(save).toHaveBeenCalledWith('Resolved'); expect(fixture.componentInstance.ticket().status).toBe('Open'); expect(fixture.componentInstance.ticket().assignedAgent?.id).toBe('old-agent');
  });
  it('rejects invalid direct close and blocked/empty submits without emitting', async () => {
    const fixture = await ready(); const save = vi.fn(); fixture.componentInstance.save.subscribe(save);
    fixture.componentInstance.submit(); fixture.componentInstance.status.setValue('Closed'); fixture.componentInstance.submit(); expect(save).not.toHaveBeenCalled();
    fixture.componentRef.setInput('blocked', true); fixture.detectChanges(); fixture.componentInstance.status.setValue('Resolved'); fixture.componentInstance.submit(); expect(save).not.toHaveBeenCalled();
  });
  it('permits reopening a closed ticket even with inactive retained references', async () => {
    const fixture = await ready('Closed'); const save = vi.fn(); fixture.componentInstance.save.subscribe(save); fixture.componentInstance.status.setValue('Open'); fixture.componentInstance.submit(); expect(save).toHaveBeenCalledWith('Open'); expect(fixture.componentInstance.ticket().category.id).toBe('old'); expect(fixture.componentInstance.ticket().assignedAgent?.id).toBe('old-agent');
  });
  it('preserves pending status on conflict reload, rejects obsolete choices, and resets only after a saved response', async () => {
    const fixture = await ready(); const save = vi.fn(); fixture.componentInstance.save.subscribe(save); fixture.componentInstance.status.setValue('InProgress');
    fixture.componentRef.setInput('ticket', { ...sampleTicket, status: 'Resolved', editVersion: 'AAAAAAAAAAI=' }); fixture.componentRef.setInput('blocked', true); fixture.detectChanges(); await fixture.whenStable(); expect(fixture.componentInstance.status.value).toBe('InProgress'); expect(fixture.nativeElement.querySelector('select').value).toBe('InProgress');
    fixture.componentRef.setInput('blocked', false); fixture.detectChanges(); fixture.componentInstance.submit(); expect(save).not.toHaveBeenCalled();
    fixture.componentInstance.status.setValue('Open'); fixture.componentInstance.submit(); expect(save).toHaveBeenCalledWith('Open'); fixture.componentRef.setInput('seed', { ...sampleTicket, editVersion: 'AAAAAAAAAAM=' }); fixture.detectChanges(); await fixture.whenStable(); expect(fixture.componentInstance.status.value).toBe('');
  });
  it('renders lifecycle history actor/time and before/after labels at this story checkpoint', async () => {
    TestBed.configureTestingModule({ imports: [TicketHistoryComponent] }); const fixture = TestBed.createComponent(TicketHistoryComponent);
    fixture.componentRef.setInput('page', { page: 1, pageSize: 25, hasMore: false, items: [{ id: 'status-entry', revision: '9007199254740993', sequence: 1, kind: 'StatusChanged', field: 'status', previousValue: 'InProgress', newValue: 'Resolved', actor: { issuer: 'issuer', subject: 'staff', displayName: 'Lifecycle Staff' }, occurredAtUtc: '2026-10-08T12:00:00Z' }] });
    fixture.detectChanges(); await fixture.whenStable(); const text = fixture.nativeElement.textContent;
    for (const value of ['StatusChanged — revision 9007199254740993', 'Status', 'In Progress', 'Resolved', 'Lifecycle Staff']) expect(text).toContain(value);
    expect(fixture.nativeElement.querySelector('time').getAttribute('datetime')).toBe('2026-10-08T12:00:00Z');
  });
});
