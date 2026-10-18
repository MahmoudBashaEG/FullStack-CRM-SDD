import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { I18n } from '../../core/i18n/i18n';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { AdminSectionNav } from './admin-section-nav';
import { Access, PERMISSION_MATRIX } from './permission-matrix';

@Component({
  selector: 'crm-permissions-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PageHeader, AdminSectionNav, TranslatePipe],
  templateUrl: './permissions-page.html',
  styleUrls: ['../../shared/ui/productivity-page.scss', './admin-page.scss'],
})
export class PermissionsPage {
  private readonly i18n = inject(I18n);
  protected readonly matrix = PERMISSION_MATRIX;

  protected symbol(access: Access): string {
    return access.level === 'full' ? '✔' : access.level === 'scoped' ? '◐' : '—';
  }

  protected label(access: Access): string {
    return access.level === 'full' ? this.i18n.t('admin.permissions.allowed') : access.level === 'scoped' ? this.i18n.t('admin.permissions.limited') : this.i18n.t('admin.permissions.notAllowed');
  }
}
