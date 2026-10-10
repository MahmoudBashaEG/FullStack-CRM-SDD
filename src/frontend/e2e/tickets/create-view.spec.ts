import { restoreTicketIdentity,closeTicketContext } from '../fixtures/ticket-local.fixture';
import { actorId,fixtureReference,createAgentReference } from '../fixtures/ticket-local.fixture';
import { test, expect, tokens, navigateTicket } from '../fixtures/auth';

test.beforeEach(async ({ request }) => {
  const readiness = '/api/ticket-reference-data/categories';
  expect((await request.get(readiness)).status()).toBe(401);
  const authorized = await request.get(readiness, { headers: { Authorization: `Bearer ${tokens().staffA}` } });
  expect(authorized.status()).toBe(200);
  expect(await authorized.json()).toContainEqual({ id: '00000000-0000-0000-0000-000000000001', displayName: 'Uncategorised' });
  const agents = await request.get('/api/ticket-reference-data/agents', { headers: { Authorization: `Bearer ${tokens().staffA}` } });
  const choices=await agents.json();expect(choices).toContainEqual({id:fixtureReference('trackingA'),displayName:'Test Agent'});expect(choices.some((choice:{id:string})=>choice.id===fixtureReference('trackingB'))).toBe(false);
});

test('creates a Unicode ticket with defaults and reads creation history from another authorized identity', async ({ page, browser }) => {
  await navigateTicket(page,'/tickets/new');
  await expect(page.getByLabel('Category', { exact: true })).toHaveValue('00000000-0000-0000-0000-000000000001');
  await expect(page.getByLabel('Priority', { exact: true })).toHaveValue('Normal');
  await expect(page.locator('option[value="30000000-0000-0000-0000-000000000002"]')).toHaveCount(0);
  await page.getByLabel('Subject', { exact: true }).fill('  مشكلة  اتصال <script>  ');
  await page.getByLabel('Description', { exact: true }).fill(' وصف\n  محفوظ ');
  const saved = page.waitForResponse(response => response.request().method() === 'POST' && response.url().endsWith('/api/tickets'));
  await page.getByRole('button', { name: 'Create ticket', exact: true }).click();
  const response = await saved; expect(response.status()).toBe(201); const detail = await response.json();
  expect(detail.status).toBe('Open'); expect(detail.priority).toBe('Normal'); expect(detail.assignedAgent).toBeNull();
  expect(detail.reference).toMatch(/^TKT-[A-F0-9]{32}$/); expect(detail.editVersion).toMatch(/^[A-Za-z0-9+/]{11}=$/);
  await expect(page).toHaveURL(new RegExp(`/tickets/${detail.reference}$`));
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(detail.reference);
  await expect(page.getByRole('heading', { name: 'Ticket history' })).toBeVisible();
  await expect(page.getByText('Created — revision 1')).toBeVisible();
  await expect(page.locator('main script')).toHaveCount(0);
  const secondContext = await browser.newContext({ baseURL: 'https://localhost:4200', ignoreHTTPSErrors: false });
  try {
    const secondPage = await secondContext.newPage(); await restoreTicketIdentity(secondPage,'staffB'); await secondPage.goto(`https://localhost:4200/tickets/${detail.reference}`);
    await expect(secondPage.getByRole('heading', { level: 1 })).toHaveText(detail.reference);
    await expect(secondPage.getByText('Created — revision 1')).toBeVisible();
    const history = await secondContext.request.get(`https://localhost:4200/api/tickets/${detail.reference}/history`, { headers: { Authorization: `Bearer ${tokens().staffB}` } });
    expect(history.status()).toBe(200); const entries = (await history.json()).items;
    expect(entries).toHaveLength(1); expect(entries[0].actor.subject).toBe(actorId());
    expect(entries[0].newValue.subject).toBe('مشكلة  اتصال <script>'); expect(entries[0].newValue.category.displayName).toBe('Uncategorised');
  } finally { await closeTicketContext(secondContext,'staffB'); }
});

test('allows an explicit category and priority and rejects blank required UI input', async ({ page }) => {
  await navigateTicket(page,'/tickets/new');
  await page.getByLabel('Subject', { exact: true }).fill('   '); await page.getByLabel('Description', { exact: true }).fill('Description');
  await page.getByRole('button', { name: 'Create ticket', exact: true }).click(); await expect(page.getByText('Subject is required.')).toBeVisible();
  await page.getByLabel('Subject', { exact: true }).fill('Selected category and priority');
  await page.getByLabel('Category', { exact: true }).selectOption('30000000-0000-0000-0000-000000000001');
  await page.getByLabel('Priority', { exact: true }).selectOption('Urgent');
  const saved = page.waitForResponse(response => response.request().method() === 'POST' && response.url().endsWith('/api/tickets'));
  await page.getByRole('button', { name: 'Create ticket', exact: true }).click(); const detail = await (await saved).json();
  expect(detail.category.displayName).toBe('Test Category'); expect(detail.priority).toBe('Urgent');
  await expect(page.getByText('Created — revision 1')).toBeVisible();
});

test('denies unauthenticated and unauthorized create/read/reference requests and guards the page', async ({ request, browser }) => {
  for (const path of ['/api/ticket-reference-data/categories', '/api/ticket-reference-data/agents', '/api/tickets/missing', '/api/tickets/missing/history']) {
    expect((await request.get(path)).status()).toBe(401);
    expect((await request.get(path, { headers: { Authorization: `Bearer ${tokens().denied}` } })).status()).toBe(403);
  }
  expect((await request.post('/api/tickets', { data: { subject: 'S', description: 'D' } })).status()).toBe(401);
  expect((await request.post('/api/tickets', { data: { subject: 'S', description: 'D' }, headers: { Authorization: `Bearer ${tokens().denied}` } })).status()).toBe(403);
  const denied = await browser.newContext({ baseURL: 'https://localhost:4200', ignoreHTTPSErrors: false });
  try {
    const deniedPage = await denied.newPage(); await restoreTicketIdentity(deniedPage,'denied'); await deniedPage.goto('https://localhost:4200/tickets/new'); await expect(deniedPage).toHaveURL(/\/access$/);
    await expect(deniedPage.getByText('Your account is signed in.')).toBeVisible();
  } finally { await closeTicketContext(denied,'denied'); }
});
