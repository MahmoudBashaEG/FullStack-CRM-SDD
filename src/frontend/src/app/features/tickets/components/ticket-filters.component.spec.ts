import { TestBed } from '@angular/core/testing';
import { TicketFiltersComponent } from './ticket-filters.component';

describe('TicketFiltersComponent', () => {
  async function fixture() {
    const result = TestBed.createComponent(TicketFiltersComponent);
    result.componentRef.setInput('criteria', { q: 'مشكلة', status: 'InProgress', categoryId: 'category', priority: 'High', assignment: 'unassigned' });
    result.componentRef.setInput('categories', [{ id: 'category', displayName: 'Inactive category', inactive: true }]);
    await result.whenStable(); result.detectChanges(); return result;
  }
  beforeEach(() => TestBed.configureTestingModule({ imports: [TicketFiltersComponent] }));
  it('restores the supplied criteria and offers read-only inactive reference filters', async () => {
    const result = await fixture(); expect(result.componentInstance.form.getRawValue()).toEqual({ q: 'مشكلة', status: 'InProgress', categoryId: 'category', priority: 'High', assignment: 'unassigned' });
    expect(result.nativeElement.textContent).toContain('Inactive category (inactive)'); expect(result.nativeElement.textContent).toContain('In Progress');
  });
  it('emits all selected filters together', async () => {
    const result = await fixture(); const values: unknown[] = []; result.componentInstance.apply.subscribe(value => values.push(value));
    result.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    expect(values).toEqual([{ q: 'مشكلة', status: 'InProgress', categoryId: 'category', priority: 'High', assignment: 'unassigned' }]);
  });
  it('clears every criterion without introducing a default status filter', async () => {
    const result = await fixture(); const values: unknown[] = []; result.componentInstance.apply.subscribe(value => values.push(value)); result.componentInstance.clearFilters();
    expect(values).toEqual([{ q: '', status: '', categoryId: '', priority: '', assignment: '' }]);
    expect(result.componentInstance.form.getRawValue()).toEqual(values[0]);
  });
});
