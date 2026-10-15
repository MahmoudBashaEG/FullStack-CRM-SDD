import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'crm-page-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<header class="page-header"><h1>{{ title() }}</h1>@if (subtitle()) {<p>{{ subtitle() }}</p>}</header>`,
  styles: `
    .page-header {
      margin-block-end: var(--crm-space-6);
    }
    h1 {
      font: var(--mat-sys-headline-medium);
      margin-block: 0 var(--crm-space-2);
    }
    p {
      font: var(--mat-sys-body-large);
      color: var(--mat-sys-on-surface-variant);
      margin: 0;
    }
  `,
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
}
