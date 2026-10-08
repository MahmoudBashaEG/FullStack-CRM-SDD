import { randomUUID } from 'node:crypto';
import type { Page } from '@playwright/test';
import { test, expect, tokens } from '../fixtures/auth';
import { provisionReferences } from '../fixtures/provision-references';

const fallback = '00000000-0000-0000-0000-000000000001';
const headers = () => ({ Authorization: `Bearer ${tokens().staffA}` });
async function change(page: Page, status: string) {
  await page.getByLabel('Ticket status', { exact: true }).selectOption(status);
  const saving = page.waitForResponse(response => response.request().method() === 'PUT' && response.url().endsWith('/status'));
  await page.getByRole('button', { name: 'Change ticket status', exact: true }).click();
  const response = await saving; expect(response.status()).toBe(200); const detail = await response.json(); expect(detail.status).toBe(status);
  await expect(page.locator('app-ticket-status select')).toHaveValue('');
  return detail;
}
test.beforeEach(async ({ request }) => {
  const readiness = '/api/ticket-reference-data/categories'; expect((await request.get(readiness)).status()).toBe(401);
  const authorized = await request.get(readiness, { headers: headers() }); expect(authorized.status()).toBe(200); expect(await authorized.json()).toContainEqual({ id: fallback, displayName: 'Uncategorised' });
});

test('creates assigns and exercises all six transitions while retaining inactive references and readable history', async ({ page, request }) => {
  const category = randomUUID(); const agent = randomUUID();
  const provision = (active: boolean) => provisionReferences({ schemaVersion: 1, categories: [{ id: category, displayName: 'Lifecycle category', isAvailable: active }], agents: [{ id: agent, issuer: 'https://example.test', subject: agent, displayName: 'Lifecycle agent', isEligible: active }] });
  provision(true);
  try {
    await page.goto('/tickets/new'); await page.getByLabel('Subject', { exact: true }).fill('طلب دعم lifecycle'); await page.getByLabel('Description', { exact: true }).fill('تفاصيل محفوظة'); await page.getByLabel('Category', { exact: true }).selectOption(category); await page.getByLabel('Priority', { exact: true }).selectOption('High');
    const creation = page.waitForResponse(response => response.request().method() === 'POST' && response.url().endsWith('/api/tickets')); await page.getByRole('button', { name: 'Create ticket', exact: true }).click(); const original = await (await creation).json();
    await expect(page).toHaveURL(new RegExp(`/tickets/${original.reference}$`));
    await page.getByLabel('Assigned agent', { exact: true }).selectOption(agent); const assignment = page.waitForResponse(response => response.request().method() === 'PUT' && response.url().endsWith('/assignment')); await page.getByRole('button', { name: 'Save assignment', exact: true }).click(); let detail = await (await assignment).json();
    const historyBefore = await (await request.get(`/api/tickets/${original.reference}/history`, { headers: headers() })).json();
    provision(false);
    const inactive = await (await request.get(`/api/tickets/${original.reference}`, { headers: headers() })).json(); expect(inactive.editVersion).toBe(detail.editVersion); expect(inactive.updatedAtUtc).toBe(detail.updatedAtUtc); expect(await (await request.get(`/api/tickets/${original.reference}/history`, { headers: headers() })).json()).toEqual(historyBefore);
    await page.reload(); await expect(page.getByLabel('Category', { exact: true })).toHaveValue(category); await expect(page.getByLabel('Assigned agent', { exact: true })).toHaveValue(agent);
    const transitions = [['Open', 'InProgress'], ['InProgress', 'Resolved'], ['Resolved', 'Open'], ['Open', 'Resolved'], ['Resolved', 'Closed'], ['Closed', 'Open']];
    for (const [from, to] of transitions) {
      expect(detail.status).toBe(from); detail = await change(page, to);
      expect(detail.reference).toBe(original.reference); expect(detail.subject).toBe(original.subject); expect(detail.description).toBe(original.description); expect(detail.priority).toBe('High'); expect(detail.createdAtUtc).toBe(original.createdAtUtc);
      expect(detail.category).toEqual({ id: category, displayName: 'Lifecycle category', isAvailable: false }); expect(detail.assignedAgent).toEqual({ id: agent, displayName: 'Lifecycle agent', isEligible: false });
      if (to === 'Closed') {
        for (const label of ['Subject', 'Description', 'Category', 'Priority', 'Assigned agent']) await expect(page.getByLabel(label, { exact: true })).toBeDisabled();
        await expect(page.getByRole('button', { name: 'Save ticket information', exact: true })).toBeDisabled(); await expect(page.getByRole('button', { name: 'Save assignment', exact: true })).toBeDisabled();
        const edit = await request.patch(`/api/tickets/${original.reference}/details`, { headers: headers(), data: { expectedVersion: detail.editVersion, subject: detail.subject } }); expect(edit.status()).toBe(409); expect((await edit.json()).code).toBe('ticket_closed');
        const assign = await request.put(`/api/tickets/${original.reference}/assignment`, { headers: headers(), data: { expectedVersion: detail.editVersion, agentId: agent } }); expect(assign.status()).toBe(409); expect((await assign.json()).code).toBe('ticket_closed');
        expect(await page.locator('#ticket-status option').evaluateAll(options => options.map(option => (option as HTMLOptionElement).value).filter(Boolean))).toEqual(['Open']);
      }
    }
    await expect(page.getByLabel('Subject', { exact: true })).toBeEnabled(); await expect(page.getByLabel('Assigned agent', { exact: true })).toHaveValue(agent);
    const history = await (await request.get(`/api/tickets/${original.reference}/history`, { headers: headers() })).json(); expect(history.items).toHaveLength(8); expect(history.items.slice(0, 2)).toEqual(historyBefore.items);
    const entries = history.items.slice(2); expect(entries.map((entry: any) => [entry.previousValue, entry.newValue])).toEqual(transitions);
    for (const [index, entry] of entries.entries()) { expect(entry.kind).toBe('StatusChanged'); expect(entry.field).toBe('status'); expect(entry.revision).toBe(String(index + 3)); expect(entry.sequence).toBe(1); expect(entry.actor).toEqual({ issuer: 'https://example.test', subject: 'staff-a', displayName: 'Test Staff' }); expect(Date.parse(entry.occurredAtUtc)).not.toBeNaN(); }
    const rows = page.locator('app-ticket-history li'); await expect(rows).toHaveCount(8);
    for (const [index, [from, to]] of transitions.entries()) { const row = rows.nth(index + 2); await expect(row).toContainText(`StatusChanged — revision ${index + 3}`); await expect(row).toContainText(from === 'InProgress' ? 'In Progress' : from); await expect(row).toContainText(to === 'InProgress' ? 'In Progress' : to); await expect(row).toContainText('Test Staff'); await expect(row.locator('time')).toHaveAttribute('datetime', entries[index].occurredAtUtc); }
  } finally { provision(false); }
});

test('an unassigned ticket can resolve close and reopen, and invalid direct close has no activity', async ({ page, request }) => {
  let detail = await (await request.post('/api/tickets', { headers: headers(), data: { subject: 'Unassigned lifecycle', description: 'No assignee prerequisite' } })).json();
  const invalid = await request.put(`/api/tickets/${detail.reference}/status`, { headers: headers(), data: { expectedVersion: detail.editVersion, status: 'Closed' } }); expect(invalid.status()).toBe(409); expect((await invalid.json()).code).toBe('invalid_transition');
  expect((await (await request.get(`/api/tickets/${detail.reference}/history`, { headers: headers() })).json()).items).toHaveLength(1);
  await page.goto(`/tickets/${detail.reference}`); await expect(page.locator('#ticket-status option[value="Closed"]')).toHaveCount(0);
  for (const status of ['Resolved', 'Closed', 'Open']) { detail = await change(page, status); expect(detail.assignedAgent).toBeNull(); }
  expect((await (await request.get(`/api/tickets/${detail.reference}/history`, { headers: headers() })).json()).items).toHaveLength(4);
});

test('stale lifecycle input preserves both drafts and requires explicit review and an allowed retry', async ({ page, request, browser }) => {
  const original = await (await request.post('/api/tickets', { headers: headers(), data: { subject: 'Concurrent lifecycle', description: 'Preserve current state' } })).json();
  const second = await browser.newContext({ ignoreHTTPSErrors: true });
  try {
    await second.addInitScript(fixture => Object.assign(window, { __CRM_TEST_AUTH: fixture }), { token: tokens().staffB, allowed: true }); const other = await second.newPage();
    await page.goto(`/tickets/${original.reference}`); await other.goto(`https://localhost:4200/tickets/${original.reference}`);
    await other.getByLabel('Subject', { exact: true }).fill('Preserved edit draft'); await other.getByLabel('Ticket status', { exact: true }).selectOption('InProgress');
    await change(page, 'Resolved'); const stale = other.waitForResponse(response => response.request().method() === 'PUT' && response.url().endsWith('/status')); await other.getByRole('button', { name: 'Change ticket status', exact: true }).click(); expect((await stale).status()).toBe(409);
    await expect(other.getByRole('alert')).toContainText('Another staff member'); await expect(other.getByLabel('Subject', { exact: true })).toHaveValue('Preserved edit draft'); await expect(other.getByLabel('Ticket status', { exact: true })).toHaveValue('InProgress'); await expect(other.getByRole('button', { name: 'Change ticket status', exact: true })).toBeDisabled();
    await other.getByRole('button', { name: 'Reload latest ticket', exact: true }).click(); await expect(other.locator('app-ticket-status option').filter({ hasText: 'draft; no longer allowed' })).toHaveCount(1); await expect(other.getByLabel('Subject', { exact: true })).toHaveValue('Preserved edit draft');
    await other.getByRole('button', { name: 'I reviewed the latest ticket', exact: true }).click(); await other.getByRole('button', { name: 'Change ticket status', exact: true }).click(); await expect(other.getByText('Choose a status allowed from the current ticket status.')).toBeVisible();
    const closed = await change(other, 'Closed'); expect(closed.subject).toBe(original.subject);
    const history = await (await request.get(`/api/tickets/${original.reference}/history`, { headers: headers() })).json(); expect(history.items).toHaveLength(3); expect(history.items[1].actor.subject).toBe('staff-a'); expect(history.items[2].actor.subject).toBe('staff-b'); expect(history.items[2].previousValue).toBe('Resolved'); expect(history.items[2].newValue).toBe('Closed');
  } finally { await second.close(); }
});

test('a lost lifecycle response requires reload and review without replaying the committed transition', async ({ page, request }) => {
  const original = await (await request.post('/api/tickets', { headers: headers(), data: { subject: 'Lost lifecycle response', description: 'Unknown outcome' } })).json();
  await page.goto(`/tickets/${original.reference}`); await page.getByLabel('Ticket status', { exact: true }).selectOption('Resolved');
  let attempts = 0; await page.route(`**/api/tickets/${original.reference}/status`, async route => { attempts++; await route.fetch(); await route.abort('failed'); }, { times: 1 });
  await page.getByRole('button', { name: 'Change ticket status', exact: true }).click(); await expect(page.getByRole('alert')).toContainText('uncertain'); expect(attempts).toBe(1); await expect(page.getByLabel('Ticket status', { exact: true })).toHaveValue('Resolved');
  const committed = await (await request.get(`/api/tickets/${original.reference}`, { headers: headers() })).json(); expect(committed.status).toBe('Resolved');
  await page.getByRole('button', { name: 'Reload latest ticket', exact: true }).click(); await expect(page.locator('#ticket-status option[value="Closed"]')).toHaveCount(1); await expect(page.getByRole('button', { name: 'Change ticket status', exact: true })).toBeDisabled();
  await page.getByRole('button', { name: 'I reviewed the latest ticket', exact: true }).click(); const noop = await request.put(`/api/tickets/${original.reference}/status`, { headers: headers(), data: { expectedVersion: committed.editVersion, status: 'Resolved' } }); expect(noop.status()).toBe(200); const same = await noop.json(); expect(same.editVersion).toBe(committed.editVersion); expect(same.updatedAtUtc).toBe(committed.updatedAtUtc);
  expect((await (await request.get(`/api/tickets/${original.reference}/history`, { headers: headers() })).json()).items).toHaveLength(2);
  await change(page, 'Closed'); expect((await (await request.get(`/api/tickets/${original.reference}/history`, { headers: headers() })).json()).items).toHaveLength(3);
});
