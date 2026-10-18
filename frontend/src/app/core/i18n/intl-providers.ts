import { MatDatepickerIntl } from '@angular/material/datepicker';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { CrmDatepickerIntl, CrmPaginatorIntl } from './material-intl';

/**
 * Translated paginator and datepicker labels. Provided on the lazy feature routes that use those components (not in app.config),
 * so the Material datepicker and paginator code stays out of the initial bundle (PLAT-001 D16).
 */
export const LAZY_INTL_PROVIDERS = [
  { provide: MatPaginatorIntl, useClass: CrmPaginatorIntl },
  { provide: MatDatepickerIntl, useClass: CrmDatepickerIntl },
];
