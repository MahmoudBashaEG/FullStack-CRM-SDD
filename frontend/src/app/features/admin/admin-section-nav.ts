import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'crm-admin-section-nav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, MatButtonModule],
  template: `
    <nav class="section-nav" aria-label="Administration sections">
      <a mat-button routerLink="/admin/users" routerLinkActive="active" ariaCurrentWhenActive="page">Users</a>
      <a mat-button routerLink="/admin/permissions" routerLinkActive="active" ariaCurrentWhenActive="page">Roles &amp; Permissions</a>
      <a mat-button routerLink="/admin/audit-logs" routerLinkActive="active" ariaCurrentWhenActive="page">Audit Logs</a>
      <a mat-button routerLink="/admin/settings" routerLinkActive="active" ariaCurrentWhenActive="page">Organization Settings</a>
    </nav>
  `,
  styles: `
    .section-nav {
      display: flex;
      flex-wrap: wrap;
      gap: var(--crm-space-2);
      margin-block-end: var(--crm-space-4);
    }
    .active {
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }
  `,
})
export class AdminSectionNav {}
