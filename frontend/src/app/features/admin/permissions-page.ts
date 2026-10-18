import { ChangeDetectionStrategy, Component } from '@angular/core';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { AdminSectionNav } from './admin-section-nav';
import { Access, PERMISSION_MATRIX } from './permission-matrix';

@Component({
  selector: 'crm-permissions-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PageHeader, AdminSectionNav],
  templateUrl: './permissions-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './admin-page.scss'],
})
export class PermissionsPage {
  protected readonly matrix = PERMISSION_MATRIX;

  protected symbol(access: Access): string {
    return access.level === 'full' ? '✔' : access.level === 'scoped' ? '◐' : '—';
  }

  protected label(access: Access): string {
    return access.level === 'full' ? 'Allowed' : access.level === 'scoped' ? 'Limited' : 'Not allowed';
  }
}
