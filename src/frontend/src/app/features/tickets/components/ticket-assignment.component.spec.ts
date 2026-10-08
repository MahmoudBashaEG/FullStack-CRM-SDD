import { TestBed } from '@angular/core/testing';
import { TicketAssignment } from './ticket-assignment.component';
import { sampleTicket } from '../testing/ticket-fixtures';
describe('TicketAssignment', () => {
  async function ready() { TestBed.configureTestingModule({ imports: [TicketAssignment] }); const fixture = TestBed.createComponent(TicketAssignment); fixture.componentRef.setInput('ticket', sampleTicket); fixture.componentRef.setInput('seed', sampleTicket); fixture.componentRef.setInput('ready', true); fixture.componentRef.setInput('agents', [{ id: 'eligible', displayName: 'Eligible' }]); fixture.detectChanges(); await fixture.whenStable(); return fixture; }
  it('retains an inactive saved ID and clears it without needing another eligible agent', async () => {
    const fixture = await ready(); const save = vi.fn(); fixture.componentInstance.save.subscribe(save); fixture.componentInstance.submit(); expect(save).toHaveBeenLastCalledWith('old-agent'); fixture.componentInstance.agent.setValue(''); fixture.componentInstance.submit(); expect(save).toHaveBeenLastCalledWith(null); expect(fixture.nativeElement.textContent).toContain('inactive, retained');
  });
  it('dispatches an actual form submission through Angular rather than navigating the document', async () => {
    const fixture = await ready(); const save = vi.fn(); fixture.componentInstance.save.subscribe(save);
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    expect(save).toHaveBeenCalledWith('old-agent');
  });
  it('rejects a different unavailable agent while allowing eligible replacement', async () => {
    const fixture = await ready(); const save = vi.fn(); fixture.componentInstance.save.subscribe(save); fixture.componentInstance.agent.setValue('other-inactive'); fixture.componentInstance.submit(); expect(save).not.toHaveBeenCalled(); expect(fixture.componentInstance.selectionError).toContain('eligible'); fixture.componentInstance.agent.setValue('eligible'); fixture.componentInstance.submit(); expect(save).toHaveBeenCalledWith('eligible');
  });
  it('preserves an assignment draft across conflict reload and guards closed tickets', async () => {
    const fixture = await ready(); const save = vi.fn(); fixture.componentInstance.save.subscribe(save); fixture.componentInstance.agent.setValue('eligible'); fixture.componentRef.setInput('ticket', { ...sampleTicket, assignedAgent: null }); fixture.componentRef.setInput('blocked', true); fixture.detectChanges(); await fixture.whenStable(); expect(fixture.componentInstance.agent.value).toBe('eligible'); fixture.componentInstance.submit(); expect(save).not.toHaveBeenCalled(); fixture.componentRef.setInput('blocked', false); fixture.componentRef.setInput('ticket', { ...sampleTicket, status: 'Closed' }); fixture.detectChanges(); fixture.componentInstance.submit(); expect(save).not.toHaveBeenCalled();
  });
});
