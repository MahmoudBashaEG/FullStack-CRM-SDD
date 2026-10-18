import { ChangeDetectionStrategy, Component, ViewEncapsulation, computed, input } from '@angular/core';
import { renderMarkdown } from './markdown';

/** Safe Markdown display used by preview, article page and ticket card. ViewEncapsulation.None so styles reach [innerHTML] content; all selectors are scoped under .crm-markdown. */
@Component({
  selector: 'crm-markdown-view',
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  template: `<div class="crm-markdown" [innerHTML]="html()"></div>`,
  styles: `
    .crm-markdown { overflow-wrap: anywhere; }
    .crm-markdown :is(h1, h2, h3, h4) { font: var(--mat-sys-title-large); margin-block: var(--crm-space-4) var(--crm-space-2); }
    .crm-markdown :is(p, ul, ol) { margin-block: 0 var(--crm-space-3); }
    .crm-markdown code { font-family: ui-monospace, monospace; background: var(--mat-sys-surface-container-high); padding-inline: var(--crm-space-1); border-radius: 4px; }
    .crm-markdown pre { overflow-x: auto; padding: var(--crm-space-3); background: var(--mat-sys-surface-container-high); border-radius: var(--crm-radius-md); }
    .crm-markdown pre code { padding: 0; background: none; }
    .crm-markdown blockquote { margin-inline: 0; padding-inline-start: var(--crm-space-3); border-inline-start: 3px solid var(--mat-sys-outline-variant); }
  `,
})
export class MarkdownView {
  readonly source = input.required<string>();
  protected readonly html = computed(() => renderMarkdown(this.source()));
}
