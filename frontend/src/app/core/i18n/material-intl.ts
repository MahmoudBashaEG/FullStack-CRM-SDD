import { Injectable, effect, inject } from '@angular/core';
import { MatDatepickerIntl } from '@angular/material/datepicker';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { I18n } from './i18n';

@Injectable()
export class CrmPaginatorIntl extends MatPaginatorIntl {
  private readonly i18n = inject(I18n);

  constructor() {
    super();
    effect(() => {
      this.i18n.language();
      const t = this.i18n.t;
      this.itemsPerPageLabel = t('paginator.itemsPerPage');
      this.nextPageLabel = t('paginator.nextPage');
      this.previousPageLabel = t('paginator.previousPage');
      this.firstPageLabel = t('paginator.firstPage');
      this.lastPageLabel = t('paginator.lastPage');
      this.getRangeLabel = (page, pageSize, length) => {
        if (length === 0 || pageSize === 0) {
          return t('paginator.rangeEmpty', { length });
        }
        const start = page * pageSize;
        const end = Math.min(start + pageSize, length);
        return t('paginator.range', { start: start + 1, end, length });
      };
      this.changes.next();
    });
  }
}

@Injectable()
export class CrmDatepickerIntl extends MatDatepickerIntl {
  private readonly i18n = inject(I18n);

  constructor() {
    super();
    effect(() => {
      this.i18n.language();
      const t = this.i18n.t;
      this.calendarLabel = t('datepicker.calendar');
      this.openCalendarLabel = t('datepicker.openCalendar');
      this.closeCalendarLabel = t('datepicker.closeCalendar');
      this.prevMonthLabel = t('datepicker.prevMonth');
      this.nextMonthLabel = t('datepicker.nextMonth');
      this.prevYearLabel = t('datepicker.prevYear');
      this.nextYearLabel = t('datepicker.nextYear');
      this.prevMultiYearLabel = t('datepicker.prevMultiYear');
      this.nextMultiYearLabel = t('datepicker.nextMultiYear');
      this.switchToMonthViewLabel = t('datepicker.switchToMonthView');
      this.switchToMultiYearViewLabel = t('datepicker.switchToMultiYearView');
      this.startDateLabel = t('datepicker.startDate');
      this.endDateLabel = t('datepicker.endDate');
      this.changes.next();
    });
  }
}
