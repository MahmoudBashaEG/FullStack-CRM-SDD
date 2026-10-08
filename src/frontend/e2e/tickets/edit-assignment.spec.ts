import { randomUUID } from 'node:crypto';
import { test, expect, tokens } from '../fixtures/auth';
import { provisionReferences } from '../fixtures/provision-references';

const fallback = '00000000-0000-0000-0000-000000000001';
const headers = () => ({ Authorization: `Bearer ${tokens().staffA}` });
test.beforeEach(async ({ request }) => {
  const readiness = '/api/ticket-reference-data/categories'; expect((await request.get(readiness)).status()).toBe(401);
  const ready = await request.get(readiness, { headers: headers() }); expect(ready.status()).toBe(200); expect(await ready.json()).toContainEqual({ id: fallback, displayName: 'Uncategorised' });
});

test('edits all fields, assigns/reassigns/clears, retains inactive references and reads exact history', async ({ page, request }) => {
  const category = randomUUID(); const agent = randomUUID(); const replacement = randomUUID();
  const provision = (active: boolean) => provisionReferences({ schemaVersion: 1, categories: [{ id: category, displayName: active ? 'Edit category' : 'Inactive edit category', isAvailable: active }], agents: [
    { id: agent, issuer: 'https://example.test', subject: agent, displayName: active ? 'Edit agent' : 'Inactive edit agent', isEligible: active },
    { id: replacement, issuer: 'https://example.test', subject: replacement, displayName: 'Replacement agent', isEligible: active }
  ] });
  provision(true);
  try {
    const created = await request.post('/api/tickets', { headers: headers(), data: { subject: 'Original edit issue', description: 'Original detail' } }); expect(created.status()).toBe(201); const initial = await created.json();
    await page.goto(`/tickets/${initial.reference}`);
    await page.getByLabel('Subject', { exact: true }).fill(' Revised issue '); await page.getByLabel('Description', { exact: true }).fill(' تفاصيل جديدة ');
    await page.getByLabel('Category', { exact: true }).selectOption(category); await page.getByLabel('Priority', { exact: true }).selectOption('High');
    const detailSave = page.waitForResponse(r => r.request().method() === 'PATCH'); await page.getByRole('button', { name: 'Save ticket information', exact: true }).click(); expect((await detailSave).status()).toBe(200);
    await expect(page.getByRole('heading', { level: 2, name: 'Revised issue', exact: true })).toBeVisible();
    const assign = async (id: string) => { await page.getByLabel('Assigned agent', { exact: true }).selectOption(id); const saving = page.waitForResponse(r => r.request().method() === 'PUT'); await page.getByRole('button', { name: 'Save assignment', exact: true }).click(); const response = await saving; expect(response.status()).toBe(200); await expect(page.getByRole('button', { name: 'Save assignment', exact: true })).toBeEnabled(); return response.json(); };
    await assign(agent); await assign(replacement); const assigned = await assign(agent);
    const historyBefore = await (await request.get(`/api/tickets/${initial.reference}/history`, { headers: headers() })).json();
    provision(false);
    const inactive = await (await request.get(`/api/tickets/${initial.reference}`, { headers: headers() })).json(); expect(inactive.editVersion).toBe(assigned.editVersion); expect(inactive.updatedAtUtc).toBe(assigned.updatedAtUtc);
    expect(await (await request.get(`/api/tickets/${initial.reference}/history`, { headers: headers() })).json()).toEqual(historyBefore);
    await page.reload(); await expect(page.getByLabel('Category', { exact: true })).toHaveValue(category); await expect(page.getByLabel('Assigned agent', { exact: true })).toHaveValue(agent);
    await expect(page.locator('#edit-category option').filter({ hasText: 'Inactive edit category (inactive, retained)' })).toHaveCount(1);
    await expect(page.locator(`#assignment-agent option[value="${replacement}"]`)).toHaveCount(0);
    await page.getByLabel('Subject', { exact: true }).fill('Unrelated edit with inactive references'); const saved = page.waitForResponse(r => r.request().method() === 'PATCH'); await page.getByRole('button', { name: 'Save ticket information', exact: true }).click(); const edited = await (await saved).json(); expect(edited.category.id).toBe(category); expect(edited.assignedAgent.id).toBe(agent);
    const retained = await assign(agent); expect(retained.editVersion).toBe(edited.editVersion); const cleared = await assign(''); expect(cleared.assignedAgent).toBeNull(); expect(cleared.status).toBe('Open');
    await page.getByLabel('Category', { exact: true }).selectOption(fallback); const replaced = page.waitForResponse(r => r.request().method() === 'PATCH'); await page.getByRole('button', { name: 'Save ticket information', exact: true }).click(); const current = await (await replaced).json();
    const rejectedCategory = await request.patch(`/api/tickets/${initial.reference}/details`, { headers: headers(), data: { expectedVersion: current.editVersion, categoryId: category, subject: 'Must not save' } }); expect(rejectedCategory.status()).toBe(409); expect((await rejectedCategory.json()).code).toBe('category_unavailable');
    const rejectedAgent = await request.put(`/api/tickets/${initial.reference}/assignment`, { headers: headers(), data: { expectedVersion: current.editVersion, agentId: agent } }); expect(rejectedAgent.status()).toBe(409); expect((await rejectedAgent.json()).code).toBe('agent_ineligible');
    const history = await (await request.get(`/api/tickets/${initial.reference}/history`, { headers: headers() })).json(); expect(history.items).toHaveLength(11);
    expect(history.items.slice(1, 5).map((entry: any) => entry.field)).toEqual(['subject', 'description', 'category', 'priority']);
    expect(history.items[1].previousValue).toBe('Original edit issue'); expect(history.items[1].newValue).toBe('Revised issue'); expect(history.items[3].newValue).toEqual({ id: category, displayName: 'Edit category' });
    expect(history.items[5].previousValue).toBeNull(); expect(history.items[5].newValue).toEqual({ id: agent, displayName: 'Edit agent' });
    expect(history.items[9].previousValue).toEqual({ id: agent, displayName: 'Inactive edit agent' }); expect(history.items[9].newValue).toBeNull(); expect(history.items[10].previousValue.displayName).toBe('Inactive edit category');
    for (const entry of history.items) { expect(entry.actor.subject).toBe('staff-a'); expect(entry.actor.displayName).toBe('Test Staff'); expect(Date.parse(entry.occurredAtUtc)).not.toBeNaN(); }
    await expect(page.getByText('DetailsChanged — revision 2')).toHaveCount(4); await expect(page.getByText('AssignmentChanged — revision 7')).toBeVisible();
    const rows = page.locator('app-ticket-history li'); await expect(rows).toHaveCount(11); await expect(rows.nth(9)).toContainText('Inactive edit agent'); await expect(rows.nth(9)).toContainText('Unassigned'); await expect(rows.nth(1)).toContainText('Original edit issue');
    await page.goto('/tickets'); await page.getByLabel('Search tickets').fill(initial.reference); await page.getByRole('button', { name: 'Apply filters', exact: true }).click();
    await expect(page.getByRole('link', { name: initial.reference, exact: true })).toBeVisible(); await expect(page.locator('tbody tr').filter({ hasText: initial.reference })).toContainText('Unrelated edit with inactive references');
  } finally { provision(false); }
});

test('concurrent staff preserve their drafts and explicitly reload review and retry', async ({ page, request, browser }) => {
  const ticket = await (await request.post('/api/tickets', { headers: headers(), data: { subject: 'Concurrent issue', description: 'Original detail' } })).json();
  const second = await browser.newContext({ ignoreHTTPSErrors: true });
  try {
    await second.addInitScript(fixture => Object.assign(window, { __CRM_TEST_AUTH: fixture }), { token: tokens().staffB, allowed: true }); const other = await second.newPage();
    await page.goto(`/tickets/${ticket.reference}`); await other.goto(`https://localhost:4200/tickets/${ticket.reference}`);
    await expect(other.getByLabel('Subject', { exact: true })).toHaveValue('Concurrent issue'); await page.getByLabel('Subject', { exact: true }).fill('First writer'); await other.getByLabel('Subject', { exact: true }).fill('Preserved draft');
    const firstSave = page.waitForResponse(r => r.request().method() === 'PATCH'); await page.getByRole('button', { name: 'Save ticket information', exact: true }).click(); expect((await firstSave).status()).toBe(200);
    const stale = other.waitForResponse(r => r.request().method() === 'PATCH'); await other.getByRole('button', { name: 'Save ticket information', exact: true }).click(); expect((await stale).status()).toBe(409);
    await expect(other.getByRole('alert')).toContainText('Another staff member'); await expect(other.getByLabel('Subject', { exact: true })).toHaveValue('Preserved draft'); await expect(other.getByRole('button', { name: 'Save ticket information', exact: true })).toBeDisabled();
    await other.getByRole('button', { name: 'Reload latest ticket', exact: true }).click(); await expect(other.getByRole('heading', { level: 2, name: 'First writer', exact: true })).toBeVisible(); await expect(other.getByLabel('Subject', { exact: true })).toHaveValue('Preserved draft');
    await other.getByRole('button', { name: 'I reviewed the latest ticket', exact: true }).click(); const retry = other.waitForResponse(r => r.request().method() === 'PATCH'); await other.getByRole('button', { name: 'Save ticket information', exact: true }).click(); expect((await retry).status()).toBe(200);
    await expect(other.getByRole('heading', { level: 2, name: 'Preserved draft', exact: true })).toBeVisible();
    const history = await (await request.get(`/api/tickets/${ticket.reference}/history`, { headers: headers() })).json(); expect(history.items).toHaveLength(3); expect(history.items[1].actor.subject).toBe('staff-a'); expect(history.items[2].actor.subject).toBe('staff-b'); expect(history.items[2].previousValue).toBe('First writer'); expect(history.items[2].newValue).toBe('Preserved draft');
  } finally { await second.close(); }
});

test('lost save response requires refresh and review before a deliberate no-op retry', async ({ page, request }) => {
  const ticket = await (await request.post('/api/tickets', { headers: headers(), data: { subject: 'Lost response', description: 'Original detail' } })).json();
  await page.goto(`/tickets/${ticket.reference}`); await page.getByLabel('Subject', { exact: true }).fill('Actually committed');
  let attempts = 0; await page.route(`**/api/tickets/${ticket.reference}/details`, async route => { attempts++; await route.fetch(); await route.abort('failed'); }, { times: 1 });
  await page.getByRole('button', { name: 'Save ticket information', exact: true }).click(); await expect(page.getByRole('alert')).toContainText('uncertain'); expect(attempts).toBe(1); await expect(page.getByLabel('Subject', { exact: true })).toHaveValue('Actually committed');
  const committed = await (await request.get(`/api/tickets/${ticket.reference}`, { headers: headers() })).json(); expect(committed.subject).toBe('Actually committed');
  await page.getByRole('button', { name: 'Reload latest ticket', exact: true }).click(); await expect(page.getByRole('heading', { level: 2, name: 'Actually committed', exact: true })).toBeVisible(); await page.getByRole('button', { name: 'I reviewed the latest ticket', exact: true }).click();
  const retry = page.waitForResponse(r => r.request().method() === 'PATCH'); await page.getByRole('button', { name: 'Save ticket information', exact: true }).click(); const result = await (await retry).json(); expect(result.editVersion).toBe(committed.editVersion); expect(result.updatedAtUtc).toBe(committed.updatedAtUtc);
  expect((await (await request.get(`/api/tickets/${ticket.reference}/history`, { headers: headers() })).json()).items).toHaveLength(2);
});
