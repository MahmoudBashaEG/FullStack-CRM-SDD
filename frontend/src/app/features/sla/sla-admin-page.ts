import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { tabQuery } from '../../shared/ui/tab-query';
import { AutomationRules } from './automation-rules';
import { BusinessCalendarForm } from './business-calendar-form';
import { SlaOverviewPanel } from './sla-overview';
import { SlaPolicyForm } from './sla-policy-form';

/** SuperAdmin "SLA & Automation" screens. Each tab loads its own data lazily when first opened; ?tab=policy|calendar|rules|overview deep-links a tab. */
@Component({
  selector: 'crm-sla-admin-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PageHeader, MatTabsModule, TranslatePipe, SlaPolicyForm, BusinessCalendarForm, AutomationRules, SlaOverviewPanel],
  templateUrl: './sla-admin-page.html',
})
export class SlaAdminPage {
  protected readonly tabs = tabQuery(['policy', 'calendar', 'rules', 'overview']);
}
