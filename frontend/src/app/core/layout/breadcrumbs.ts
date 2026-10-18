import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRouteSnapshot, NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter, map, startWith } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { I18n } from '../i18n/i18n';
import { TranslatePipe } from '../i18n/translate.pipe';

interface Crumb {
  key: string;
  url: string;
}

/**
 * Breadcrumbs from the router: Home › section (route data `crumb`, a translation key) › page (a child route that has a `title` key
 * and a non-empty path). Hidden below 600 px; the page header shows the page title there.
 */
@Component({
  selector: 'crm-breadcrumbs',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatIconModule, TranslatePipe],
  template: `
    <nav [attr.aria-label]="'shell.breadcrumbs' | t">
      <ol>
        @for (crumb of crumbs(); track crumb.url; let last = $last) {
          <li>
            @if (last) {
              <span aria-current="page">{{ i18n.t(crumb.key) }}</span>
            } @else {
              <a [routerLink]="crumb.url">{{ i18n.t(crumb.key) }}</a>
              <mat-icon class="dir-icon" aria-hidden="true">chevron_right</mat-icon>
            }
          </li>
        }
      </ol>
    </nav>
  `,
  styles: `
    ol {
      display: flex;
      align-items: center;
      flex-wrap: nowrap;
      margin: 0;
      padding: 0;
      list-style: none;
      font: var(--mat-sys-label-large);
      color: var(--mat-sys-on-surface-variant);
    }
    li {
      display: inline-flex;
      align-items: center;
      white-space: nowrap;
    }
    a {
      color: inherit;
      text-decoration: none;
    }
    a:hover {
      color: var(--mat-sys-primary);
      text-decoration: underline;
    }
    [aria-current='page'] {
      color: var(--mat-sys-on-surface);
    }
    mat-icon {
      font-size: 18px;
      inline-size: 18px;
      block-size: 18px;
    }
    @media (max-width: 599.98px) {
      :host {
        display: none;
      }
    }
  `,
})
export class Breadcrumbs {
  protected readonly i18n = inject(I18n);
  private readonly router = inject(Router);
  private readonly navigated = toSignal(
    this.router.events.pipe(filter((event) => event instanceof NavigationEnd), map(() => this.router.routerState.snapshot.root), startWith(this.router.routerState.snapshot.root)),
    { requireSync: true },
  );

  protected readonly crumbs = computed<Crumb[]>(() => {
    const crumbs: Crumb[] = [];
    let url = '';
    let route: ActivatedRouteSnapshot | null = this.navigated();
    while (route) {
      const segment = route.url.map((s) => s.path).join('/');
      if (segment) {
        url += `/${segment}`;
      }
      const data = route.routeConfig?.data?.['crumb'] as string | undefined; // own config only: route.data is inherited by child routes
      const title = route.routeConfig?.title;
      if (data) {
        crumbs.push({ key: data, url: url || '/' });
      } else if (segment && typeof title === 'string') {
        crumbs.push({ key: title, url });
      }
      route = route.firstChild;
    }
    return crumbs.length > 0 && crumbs[0].url === '/' ? crumbs : [{ key: 'nav.home', url: '/' }, ...crumbs].filter(
      (crumb, index, all) => index === 0 || crumb.url !== all[index - 1].url,
    );
  });
}
