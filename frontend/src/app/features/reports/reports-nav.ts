import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/** Sub-navigation of the five report screens. The period query params travel with every link. */
@Component({
  selector: 'crm-reports-nav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatTabsModule, RouterLink, RouterLinkActive, TranslatePipe],
  template: `
    <nav mat-tab-nav-bar mat-stretch-tabs="false" [tabPanel]="panel" [attr.aria-label]="'reports.nav.label' | t">
      @for (link of links; track link.path) {
        <a
          mat-tab-link
          [routerLink]="link.path"
          queryParamsHandling="preserve"
          routerLinkActive
          #rla="routerLinkActive"
          [routerLinkActiveOptions]="match"
          [active]="rla.isActive"
        >
          {{ link.key | t }}
        </a>
      }
    </nav>
    <mat-tab-nav-panel #panel class="panel" />
  `,
  styles: `
    .panel { display: block; }
    :host {
      display: block;
      margin-block-end: var(--crm-space-4);
      overflow-x: auto;
    }
  `,
})
export class ReportsNav {
  protected readonly match = { paths: 'exact', queryParams: 'ignored', fragment: 'ignored', matrixParams: 'ignored' } as const;
  protected readonly links = [
    { path: '/reports', key: 'reports.nav.overview' },
    { path: '/reports/tickets', key: 'reports.nav.tickets' },
    { path: '/reports/sla', key: 'reports.nav.sla' },
    { path: '/reports/agents', key: 'reports.nav.agents' },
    { path: '/reports/customer-satisfaction', key: 'reports.nav.csat' },
  ];
}
