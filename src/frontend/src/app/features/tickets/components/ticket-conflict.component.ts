import { Component, input, output } from '@angular/core';
@Component({ selector: 'app-ticket-conflict', template: `<section aria-label="Save feedback">
  @if (message()) { <p role="alert">{{ message() }}</p> }
  @if (needsReload() || review()) { <button type="button" [disabled]="busy()" (click)="reload.emit()">Reload latest ticket</button> }
  @if (review() && !reviewed()) { <button type="button" [disabled]="busy()" (click)="confirm.emit()">I reviewed the latest ticket</button> }
  @if (reviewed()) { <p>Review complete. Use the save button to explicitly resubmit your draft.</p> }
</section>` })
export class TicketConflict {
  readonly message = input(''); readonly needsReload = input(false); readonly review = input(false); readonly reviewed = input(false); readonly busy = input(false);
  readonly reload = output<void>(); readonly confirm = output<void>();
}
