import { Marked, Tokens } from 'marked';

const SAFE_LINK = /^(https?:|mailto:)/i;

function escapeHtml(text: string): string {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

// Private instance: never mutates the global `marked` defaults.
const markdown = new Marked({
  gfm: true,
  renderer: {
    // Author-provided HTML (block or inline) is shown as text, never rendered (K3).
    html({ text }: Tokens.HTML | Tokens.Tag): string {
      return escapeHtml(text);
    },
    // No embedded images in KB-001: show the alt text only.
    image({ text }: Tokens.Image): string {
      return escapeHtml(text);
    },
    // Only http(s) and mailto links; anything else (javascript:, data:, relative) is rendered as its plain label.
    link({ href, title, tokens }: Tokens.Link): string {
      const label = this.parser.parseInline(tokens);
      if (!SAFE_LINK.test(href.trim())) {
        return label;
      }
      const titleAttr = title ? ` title="${escapeHtml(title)}"` : '';
      return `<a href="${escapeHtml(href)}"${titleAttr} target="_blank" rel="noopener noreferrer">${label}</a>`;
    },
  },
});

/** Markdown source → HTML string for MarkdownView. Bound through [innerHTML], so Angular's sanitizer runs as a second layer. */
export function renderMarkdown(source: string): string {
  return markdown.parse(source, { async: false });
}
