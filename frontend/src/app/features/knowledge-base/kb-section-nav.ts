import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../core/auth/auth-service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/** Articles / Categories tabs for the Knowledge Base. Categories is shown to SuperAdmin only (the route guard stays). Page content is projected into the tab panel. */
@Component({
  selector: 'crm-kb-section-nav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatTabsModule, RouterLink, RouterLinkActive, TranslatePipe],
  template: `
    <nav mat-tab-nav-bar [tabPanel]="panel" [attr.aria-label]="'kb.nav.label' | t">
      <a
        mat-tab-link
        routerLink="/knowledge-base"
        routerLinkActive
        #articles="routerLinkActive"
        [routerLinkActiveOptions]="{ exact: true }"
        [active]="articles.isActive">
        {{ 'kb.nav.articles' | t }}
      </a>
      @if (isSuperAdmin()) {
        <a
          mat-tab-link
          routerLink="/knowledge-base/categories"
          routerLinkActive
          #categories="routerLinkActive"
          [routerLinkActiveOptions]="{ exact: true }"
          [active]="categories.isActive">
          {{ 'kb.nav.categories' | t }}
        </a>
      }
    </nav>
    <mat-tab-nav-panel #panel class="panel"><ng-content /></mat-tab-nav-panel>
  `,
  styles: `
    :host {
      display: block;
    }
    .panel {
      display: block;
      padding-block-start: var(--crm-space-4);
    }
  `,
})
export class KbSectionNav {
  private readonly auth = inject(AuthService);
  protected readonly isSuperAdmin = computed(() => this.auth.role() === 'SuperAdmin');
}
