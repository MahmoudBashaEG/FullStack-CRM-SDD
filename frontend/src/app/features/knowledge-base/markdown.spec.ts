import { renderMarkdown } from './markdown';

describe('renderMarkdown', () => {
  it('renders headings, ordered steps, emphasis, code and safe links, and shows raw HTML as text', () => {
    const html = renderMarkdown(
      [
        '## Reset a password',
        '',
        '1. Open **Agents**',
        '2. Choose *Set password*',
        '',
        'Run `reset` and read [the guide](https://example.com/guide).',
        '',
        '<script>alert(1)</script>',
        '',
        '[bad link](javascript:alert(1))',
      ].join('\n'),
    );

    expect(html).toContain('<h2>Reset a password</h2>');
    expect(html).toContain('<ol>');
    expect(html).toContain('<strong>Agents</strong>');
    expect(html).toContain('<em>Set password</em>');
    expect(html).toContain('<code>reset</code>');
    expect(html).toContain('<a href="https://example.com/guide" target="_blank" rel="noopener noreferrer">the guide</a>');
    expect(html).toContain('&lt;script&gt;');
    expect(html).not.toContain('<script');
    expect(html).not.toContain('javascript:');
    expect(html).toContain('bad link');
  });
});
