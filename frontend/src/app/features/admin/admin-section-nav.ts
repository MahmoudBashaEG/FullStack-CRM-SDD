import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/** Route-backed section tabs. The page content is projected into the tab panel (aria-controls / labelledby wiring). */
@Component({
  selector: 'crm-admin-section-nav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, MatTabsModule, TranslatePipe],
  template: `
    <nav mat-tab-nav-bar [tabPanel]="panel" [attr.aria-label]="'admin.nav.label' | t">
      <a mat-tab-link routerLink="/admin/users" routerLinkActive #users="routerLinkActive" [active]="users.isActive">
        {{ 'admin.nav.users' | t }}
      </a>
      <a mat-tab-link routerLink="/admin/permissions" routerLinkActive #permissions="routerLinkActive" [active]="permissions.isActive">
        {{ 'admin.nav.permissions' | t }}
      </a>
      <a mat-tab-link routerLink="/admin/audit-logs" routerLinkActive #audit="routerLinkActive" [active]="audit.isActive">
        {{ 'admin.nav.auditLogs' | t }}
      </a>
      <a mat-tab-link routerLink="/admin/settings" routerLinkActive #settings="routerLinkActive" [active]="settings.isActive">
        {{ 'admin.nav.settings' | t }}
      </a>
    </nav>
    <mat-tab-nav-panel #panel class="panel">
      <ng-content />
    </mat-tab-nav-panel>
  `,
  styles: `
    :host {
      display: block;
    }
    .panel {
      display: block;
      padding-block-start: var(--crm-space-5);
    }
  `,
})
export class AdminSectionNav {}
