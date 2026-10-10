import { restoreTicketIdentity,closeTicketContext } from '../fixtures/ticket-local.fixture';
import { actorId,fixtureReference,createAgentReference } from '../fixtures/ticket-local.fixture';
import { test, expect, tokens, navigateTicket } from '../fixtures/auth';

test('keyboard-only creation preserves Arabic/English text and exposes visible focus', async ({ page }) => {
  await navigateTicket(page,'/tickets/new');
  const subject = page.getByLabel('Subject', { exact: true });
  for (let index = 0; index < 12 && !(await subject.evaluate(element => element === document.activeElement)); index++) await page.keyboard.press('Tab');
  await expect(subject).toBeFocused();
  expect(await subject.evaluate(element => getComputedStyle(element).outlineStyle)).not.toBe('none');
  await page.keyboard.insertText('مشكلة اتصال — English support');
  await page.keyboard.press('Tab'); await expect(page.getByLabel('Description', { exact: true })).toBeFocused();
  await page.keyboard.insertText('وصف محفوظ\nEnglish details <script>plain text</script>');
  await page.keyboard.press('Tab'); await expect(page.getByLabel('Category', { exact: true })).toBeFocused();
  await page.keyboard.press('Tab'); await expect(page.getByLabel('Priority', { exact: true })).toBeFocused();
  await page.keyboard.press('Tab'); await expect(page.getByRole('button', { name: 'Create ticket', exact: true })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(/^TKT-/);
  await expect(page.getByRole('heading', { name: 'مشكلة اتصال — English support', exact: true })).toBeVisible();
  await expect(page.locator('.description')).toHaveText('وصف محفوظ\nEnglish details <script>plain text</script>');
  await expect(page.locator('main script')).toHaveCount(0);
  await expect(page.getByText('Created — revision 1')).toBeVisible();
  // Editing, assignment, lifecycle and history controls use the same native keyboard controls.
  for (const label of ['Subject', 'Description', 'Category', 'Priority', 'Assigned agent', 'Ticket status']) {
    const control = page.getByLabel(label, { exact: true }); await control.focus(); await expect(control).toBeFocused();
  }
});

for (const direction of ['ltr', 'rtl'] as const) {
  test(`narrow ${direction} pages retain content and controls without document overflow; branding is configurable`, async ({ page, request }) => {
    await page.evaluate(dir => { document.documentElement.dir = dir; }, direction);
    const subject = 'بلاغ'.repeat(100) + 'English'.repeat(30);
    const created = await request.post('/api/tickets', { data: { subject, description: 'نص طويل محفوظ\nEnglish description' }, headers: { Authorization: `Bearer ${tokens().staffA}` } });
    expect(created.status()).toBe(201); const ticket = await created.json();
    for (const width of [320, 375, 768]) {
      await page.setViewportSize({ width, height: 900 });
      for (const route of ['/tickets/new', `/tickets/${ticket.reference}`, '/tickets']) {
        await navigateTicket(page,route);
        await expect(page.locator('html')).toHaveAttribute('dir', direction);
        if (route.endsWith(ticket.reference)) await expect(page.getByText('Created — revision 1')).toBeVisible();
        else if (route === '/tickets/new') await expect(page.getByRole('button', { name: 'Create ticket', exact: true })).toBeEnabled();
        else await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
        expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth), `${route} at ${width}px ${direction}`).toBeLessThanOrEqual(1);
        await page.evaluate(() => document.documentElement.style.setProperty('--brand-color', 'rgb(80, 30, 100)'));
        await expect(page.locator('header')).toHaveCSS('background-color', 'rgb(80, 30, 100)');
      }
    }
  });
}
