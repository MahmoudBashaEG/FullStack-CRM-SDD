import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { Branding } from '../../branding/branding';
import { AuthService } from '../../auth/auth-service';
import { I18n } from '../../i18n/i18n';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { NotificationBell } from '../../notifications/notification-bell';
import { readPreference, writePreference } from '../../preferences/preferences-store';
import { Breadcrumbs } from '../breadcrumbs';
import { NAV_GROUP_ORDER, NAV_ITEMS, NavGroup, NavItem } from '../navigation';
import { TopbarControls } from '../topbar-controls';

/** PLAT-001 shell: grouped, role-filtered sidebar (mini rail on desktop, drawer on handset), top bar with breadcrumbs, switchers and user menu. */
@Component({
  selector: 'crm-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatTooltipModule,
    NotificationBell,
    Breadcrumbs,
    TopbarControls,
    TranslatePipe,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  private readonly auth = inject(AuthService);
  protected readonly branding = inject(Branding);
  protected readonly i18n = inject(I18n);
  protected readonly user = this.auth.currentUser;
  protected readonly isStaff = computed(() => this.auth.role() === 'Agent' || this.auth.role() === 'SuperAdmin');
  protected readonly collapsed = signal(readPreference('crm.navCollapsed') === '1');

  protected readonly isHandset = toSignal(
    inject(BreakpointObserver).observe(Breakpoints.Handset).pipe(map((result) => result.matches)),
    { initialValue: false },
  );
  /** Rail mode only exists on desktop; the handset drawer always shows full labels. */
  protected readonly rail = computed(() => this.collapsed() && !this.isHandset());

  protected readonly navGroups = computed<{ group: NavGroup; items: NavItem[] }[]>(() => {
    const role = this.auth.role();
    const visible = NAV_ITEMS.filter((item) => !item.roles || (role !== null && item.roles.includes(role)));
    return NAV_GROUP_ORDER.map((group) => ({ group, items: visible.filter((item) => item.group === group) })).filter((g) => g.items.length > 0);
  });

  protected readonly initials = computed(() => {
    const user = this.user();
    return user ? `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`.toUpperCase() : '';
  });

  protected toggleCollapsed(): void {
    this.collapsed.update((value) => !value);
    writePreference('crm.navCollapsed', this.collapsed() ? '1' : null);
  }

  protected signOut(): void {
    this.auth.logout();
  }
}
