import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../../auth/auth-service';
import { NotificationBell } from '../../notifications/notification-bell';
import { NAV_ITEMS } from '../navigation';

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
    NotificationBell,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  private readonly auth = inject(AuthService);
  protected readonly user = this.auth.currentUser;
  protected readonly isStaff = computed(() => this.auth.role() === 'Agent' || this.auth.role() === 'SuperAdmin');
  protected readonly navItems = computed(() => {
    const role = this.auth.role();
    return NAV_ITEMS.filter(item => !item.roles || (role !== null && item.roles.includes(role)));
  });

  protected signOut(): void {
    this.auth.logout();
  }

  protected readonly isHandset = toSignal(
    inject(BreakpointObserver).observe(Breakpoints.Handset).pipe(map(result => result.matches)),
    { initialValue: false },
  );
}
