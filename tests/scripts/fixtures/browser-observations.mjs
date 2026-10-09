import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
const root = fileURLToPath(new URL('../../../', import.meta.url));
const { chromium } = await import(new URL('../../../src/frontend/node_modules/playwright-core/index.mjs', import.meta.url));
const browser = await chromium.launch({ headless: true });
const source = root + 'src/frontend/src/app/core/auth/access.page.ts';
const original = readFileSync(source);
try {
  const page = await browser.newPage(); // Normal TLS, normal adapter; no token/session injection.
  await page.goto('https://localhost:4200/tickets/new');
  await page.waitForURL('**/access');
  await page.getByRole('heading', { name: 'Support queue access', exact: true }).waitFor();
  assert.equal(await page.getByRole('button', { name: 'Sign in', exact: true }).count(), 1);
  writeFileSync(source, original.toString().replace('<h1>Support queue access</h1>', '<h1>Support queue access reload probe</h1>'));
  await page.getByRole('heading', { name: 'Support queue access reload probe', exact: true }).waitFor({ timeout: 30000 });
  writeFileSync(source, original);
  await page.getByRole('heading', { name: 'Support queue access', exact: true }).waitFor({ timeout: 30000 });
  console.log('PASS: normal browser trusted HTTPS, unauthenticated guard/sign-in page, Angular live reload and exact source restoration');
} finally { writeFileSync(source, original); await browser.close(); }
