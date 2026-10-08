import { TestBed } from '@angular/core/testing';
import { TicketEditForm } from './ticket-edit-form.component';
import { sampleTicket } from '../testing/ticket-fixtures';
describe('TicketEditForm', () => {
  async function ready() { TestBed.configureTestingModule({ imports: [TicketEditForm] }); const fixture = TestBed.createComponent(TicketEditForm); fixture.componentRef.setInput('ticket', sampleTicket); fixture.componentRef.setInput('seed', sampleTicket); fixture.componentRef.setInput('ready', true); fixture.componentRef.setInput('categories', [{ id: 'available', displayName: 'Available' }]); fixture.detectChanges(); await fixture.whenStable(); return fixture; }
  it('retains current inactive category and emits only changed fields, including normalized no-op input', async () => {
    const fixture = await ready(); const save = vi.fn(); fixture.componentInstance.save.subscribe(save); fixture.componentInstance.form.patchValue({ subject: '  Saved  ' }); fixture.componentInstance.submit(); expect(save).toHaveBeenCalledWith({ subject: '  Saved  ' });
    expect(fixture.nativeElement.textContent).toContain('Old (inactive, retained)'); expect(fixture.componentInstance.form.controls.categoryId.value).toBe('old');
  });
  it('rejects newly selected unavailable IDs and blank required text before emitting', async () => {
    const fixture = await ready(); const save = vi.fn(); fixture.componentInstance.save.subscribe(save); fixture.componentInstance.form.patchValue({ categoryId: 'other-inactive' }); fixture.componentInstance.submit(); expect(save).not.toHaveBeenCalled(); expect(fixture.componentInstance.selectionError).toContain('available');
    fixture.componentInstance.form.patchValue({ categoryId: 'available', subject: '  ' }); fixture.componentInstance.submit(); expect(save).not.toHaveBeenCalled(); expect(fixture.componentInstance.form.controls.subject.invalid).toBe(true);
  });
  it('preserves dirty draft through reload and emits its explicit intent after review', async () => {
    const fixture = await ready(); const save = vi.fn(); fixture.componentInstance.save.subscribe(save); fixture.componentInstance.form.controls.subject.setValue('My draft'); fixture.componentInstance.form.controls.subject.markAsDirty();
    fixture.componentRef.setInput('ticket', { ...sampleTicket, subject: 'Other writer', description: 'Latest description', editVersion: 'AAAAAAAAAAI=' }); fixture.componentRef.setInput('blocked', true); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.componentInstance.form.controls.subject.value).toBe('My draft'); fixture.componentInstance.submit(); expect(save).not.toHaveBeenCalled(); fixture.componentRef.setInput('blocked', false); fixture.detectChanges(); fixture.componentInstance.submit(); expect(save).toHaveBeenCalledWith({ subject: 'My draft' });
  });
  it('closed blocks even no-op and new committed seed resets the form', async () => {
    const fixture = await ready(); const save = vi.fn(); fixture.componentInstance.save.subscribe(save); fixture.componentRef.setInput('ticket', { ...sampleTicket, status: 'Closed' }); fixture.detectChanges(); fixture.componentInstance.submit(); expect(save).not.toHaveBeenCalled();
    fixture.componentRef.setInput('seed', { ...sampleTicket, subject: 'Committed' }); fixture.detectChanges(); await fixture.whenStable(); expect(fixture.componentInstance.form.controls.subject.value).toBe('Committed');
  });
});
