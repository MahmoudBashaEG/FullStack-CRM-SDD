import { test, expect, tokens } from '../fixtures/auth';

test('an 8001-character literal search reaches the real API and renders matching and empty results', async ({ page, request }) => {
  const prefix = `LongBrowser-${Date.now()}-`;
  const term = prefix + 'a'.repeat(8000 - prefix.length) + 'Z';
  expect(term.length).toBe(8001);
  const created = await request.post('/api/tickets', { data: { subject: `before ${term} after`, description: 'Synthetic browser long-search regression' }, headers: { Authorization: `Bearer ${tokens().staffA}` } });
  expect(created.status()).toBe(201); const ticket = await created.json();
  await page.goto('/tickets');
  await page.getByLabel('Search tickets').fill(term);
  let loaded = page.waitForResponse(response => new URL(response.url()).pathname === '/api/tickets' && response.request().method() === 'GET' && new URL(response.url()).searchParams.get('q') === term);
  await page.getByRole('button', { name: 'Apply filters' }).click(); expect((await loaded).status()).toBe(200);
  await expect(page.locator('tbody tr')).toHaveCount(1);
  await expect(page.getByRole('link', { name: ticket.reference, exact: true })).toBeVisible();
  expect(new URL(page.url()).searchParams.get('q')).toBe(term);
  const missing = term.slice(0, -1) + 'Y'; await page.getByLabel('Search tickets').fill(missing);
  loaded = page.waitForResponse(response => new URL(response.url()).pathname === '/api/tickets' && response.request().method() === 'GET' && new URL(response.url()).searchParams.get('q') === missing);
  await page.getByRole('button', { name: 'Apply filters' }).click(); expect((await loaded).status()).toBe(200);
  await expect(page.getByText('No tickets match these filters.')).toBeVisible(); await expect(page.getByRole('alert')).toHaveCount(0);
});
import type { TicketSummary } from '../../src/app/features/tickets/models/ticket.models';

const categoryA = '30000000-0000-0000-0000-000000000001';
const categoryB = '30000000-0000-0000-0000-000000000002';
const fallback = '00000000-0000-0000-0000-000000000001';
const agentA = '20000000-0000-0000-0000-000000000001';
const agentB = '20000000-0000-0000-0000-000000000002';
const reference = (index: number) => `TKT-10000000000000000000${String(index).padStart(12, '0')}`;
const primary = [
  ['TrackingSeed Alpha Service', 'Open', 'Normal', categoryA, agentA, 12],
  ['TrackingSeed alpha hardware', 'InProgress', 'High', categoryA, agentB, 12],
  ['TrackingSeed مشكلة اتصال', 'Resolved', 'High', categoryB, agentB, 11],
  ['TrackingSeed literal %_[brackets]\\ mark', 'Closed', 'Low', fallback, '', 11],
  ['TrackingSeed ALPHA café', 'Closed', 'Normal', categoryA, '', 10],
  ['TrackingSeed Beta', 'Open', 'Urgent', categoryB, agentA, 12],
  ['TrackingSeed Gamma', 'Resolved', 'Low', fallback, agentB, 10],
  [`TrackingSeed reference ${reference(1)}`, 'InProgress', 'High', fallback, '', 10],
] as const;
const records = Array.from({ length: 32 }, (_, offset) => {
  const index = offset + 1;
  const values = index <= 8 ? primary[offset] : [`TrackingSeed Paging ${index}`, ['Open', 'InProgress', 'Resolved', 'Closed'][index % 4], ['Low', 'Normal', 'High', 'Urgent'][index % 4], [fallback, categoryA, categoryB][index % 3], ['', agentA, agentB][index % 3], 9];
  return { reference: reference(index), subject: String(values[0]), status: String(values[1]), priority: String(values[2]), categoryId: String(values[3]), agentId: String(values[4]), updatedAt: Date.parse(`2026-10-08T${String(values[5]).padStart(2, '0')}:00:00Z`) };
}).sort((left, right) => right.updatedAt - left.updatedAt || left.reference.localeCompare(right.reference));
const headers = () => ({ Authorization: `Bearer ${tokens().staffA}` });

test.beforeEach(async ({ request }) => {
  const url = '/api/ticket-reference-data/categories'; expect((await request.get(url)).status()).toBe(401);
  expect((await request.get(url, { headers: headers() })).status()).toBe(200);
});

test('every search/filter combination matches the prepared-record oracle in API and browser', async ({ page, request }) => {
  await page.goto('/tickets?q=TrackingSeed&pageSize=100'); await expect(page.locator('tbody tr')).toHaveCount(32);
  // US1 runs in the same owned queue and creates additional tickets. They must also match searches.
  const baselineResponse = await request.get('/api/tickets?pageSize=100', { headers: headers() });
  const baseline = await baselineResponse.json(); expect(baseline.hasMore).toBe(false);
  const fixtureIds = new Set(records.map(record => record.reference));
  expect((baseline.items as TicketSummary[]).filter(row => fixtureIds.has(row.reference))).toHaveLength(32);
  const extraRecords = (baseline.items as TicketSummary[]).filter(row => !fixtureIds.has(row.reference)).map(row => ({
    reference: row.reference, subject: row.subject, status: row.status, priority: row.priority, categoryId: row.category.id,
    agentId: row.assignedAgent?.id ?? '', updatedAt: Date.parse(row.updatedAtUtc),
  }));
  const oracle = [...records, ...extraRecords].sort((left, right) => right.updatedAt - left.updatedAt || left.reference.localeCompare(right.reference));
  const cases = [
    { q: 'TrackingSeed' }, { q: 'ALPHA' }, { q: 'مشكلة' }, { q: '%' }, { q: '_' }, { q: '[' }, { q: ']' }, { q: '\\' },
    { q: reference(1) }, { q: reference(1).toLowerCase() }, { q: 'TKT-100000000000' }, { q: 'No such ticket' },
    { q: 'TrackingSeed', status: 'Open' }, { q: 'TrackingSeed', status: 'InProgress' }, { q: 'TrackingSeed', status: 'Resolved' }, { q: 'TrackingSeed', status: 'Closed' },
    { q: 'TrackingSeed', priority: 'High' }, { q: 'TrackingSeed', categoryId: categoryB },
    { q: 'TrackingSeed', assignment: 'unassigned' }, { q: 'TrackingSeed', assignment: agentB },
    { q: 'alpha', status: 'Open', priority: 'Normal', categoryId: categoryA, assignment: agentA },
  ];
  for (const criteria of cases) {
    const filter = { q: '', status: '', priority: '', categoryId: '', assignment: '', ...criteria };
    const expected = oracle.filter(record => (!filter.q.trim() || record.reference === filter.q.trim() || record.subject.toLowerCase().includes(filter.q.trim().toLowerCase())) &&
      (!filter.status || record.status === filter.status) && (!filter.priority || record.priority === filter.priority) && (!filter.categoryId || record.categoryId === filter.categoryId) &&
      (!filter.assignment || (filter.assignment === 'unassigned' ? !record.agentId : record.agentId === filter.assignment))).map(record => record.reference);
    const query = new URLSearchParams({ ...Object.fromEntries(Object.entries(filter).filter(([, value]) => value !== '')), pageSize: '100' });
    const response = await request.get('/api/tickets?' + query, { headers: headers() }); expect(response.status()).toBe(200);
    expect((await response.json()).items.map((item: { reference: string }) => item.reference)).toEqual(expected);
    await page.getByLabel('Search tickets').fill(filter.q); await page.getByLabel('Status filter').selectOption(filter.status);
    await page.getByLabel('Category filter').selectOption(filter.categoryId); await page.getByLabel('Priority filter').selectOption(filter.priority); await page.getByLabel('Assignment filter').selectOption(filter.assignment);
    const loaded = page.waitForResponse(response => new URL(response.url()).pathname === '/api/tickets' && response.request().method() === 'GET');
    await page.getByRole('button', { name: 'Apply filters' }).click(); expect((await loaded).status()).toBe(200);
    await expect(page.locator('tbody tr')).toHaveCount(expected.length);
    expect(await page.locator('tbody td:first-child a').allTextContents()).toEqual(expected);
    if (expected.length === 0) await expect(page.getByText('No tickets match these filters.')).toBeVisible();
  }
  const allReferences = oracle.map(row => row.reference);
  await page.getByRole('button', { name: 'Clear filters' }).click(); await expect(page.locator('tbody tr')).toHaveCount(allReferences.length);
  expect(await page.locator('tbody td:first-child a').allTextContents()).toEqual(allReferences);
});

test('paging retains criteria, handles ties, and navigates to readable ticket detail', async ({ page }) => {
  await page.goto('/tickets?q=TrackingSeed'); await expect(page.locator('tbody tr')).toHaveCount(25);
  expect(await page.locator('tbody td:first-child a').allTextContents()).toEqual(records.slice(0, 25).map(record => record.reference));
  await page.getByRole('button', { name: 'Next tickets' }).click(); await expect(page.locator('tbody tr')).toHaveCount(7);
  expect(await page.locator('tbody td:first-child a').allTextContents()).toEqual(records.slice(25).map(record => record.reference));
  expect(new URL(page.url()).searchParams.get('q')).toBe('TrackingSeed'); expect(new URL(page.url()).searchParams.get('page')).toBe('2');
  await page.getByRole('button', { name: 'Previous tickets' }).click(); await expect(page.locator('tbody tr')).toHaveCount(25);
  await page.getByRole('link', { name: reference(1), exact: true }).click(); await expect(page.getByRole('heading', { level: 1 })).toHaveText(reference(1));
  await expect(page.getByText('Created — revision 1')).toBeVisible(); await page.goBack(); await expect(page.locator('tbody tr')).toHaveCount(25);
});

test('invalid filters and server failures stay distinct from empty success; access remains protected', async ({ page, request, browser }) => {
  await page.goto('/tickets?page=0x2'); await expect(page.getByRole('alert')).toContainText('Some filters are invalid');
  await page.goto('/tickets?q=NoSuchPreparedTicket'); await expect(page.getByText('No tickets match these filters.')).toBeVisible();
  await page.route('**/api/tickets?*', route => route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ code: 'service_unavailable' }) }));
  await page.goto('/tickets?q=TrackingSeed'); await expect(page.getByRole('alert')).toContainText('Tickets could not be loaded'); await expect(page.getByText('No tickets match these filters.')).toHaveCount(0);
  await page.unroute('**/api/tickets?*'); await page.getByRole('button', { name: 'Retry tickets' }).click(); await expect(page.locator('tbody tr')).toHaveCount(25);
  expect((await request.get('/api/tickets')).status()).toBe(401); expect((await request.get('/api/tickets', { headers: { Authorization: `Bearer ${tokens().denied}` } })).status()).toBe(403);
  const denied = await browser.newContext({ ignoreHTTPSErrors: true });
  try { await denied.addInitScript(fixture => Object.assign(window, { __CRM_TEST_AUTH: fixture }), { token: tokens().denied, allowed: false }); const deniedPage = await denied.newPage();
    await deniedPage.goto('https://localhost:4200/tickets'); await expect(deniedPage).toHaveURL(/\/access$/);
  } finally { await denied.close(); }
});
