import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

/** Lets the inbox ask the bell to re-check immediately (e.g. after marking a reminder read). */
@Injectable({ providedIn: 'root' })
export class ReminderBadgeRefresh {
  readonly requests = new Subject<void>();

  next(): void {
    this.requests.next();
  }
}
