import { test as base, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';

export const tokens = () => JSON.parse(readFileSync(process.env['CRM_E2E_TOKEN_FILE']!, 'utf8')) as { staffA: string; staffB: string; denied: string };
export const test = base.extend({ page: async ({ page }, use) => {
  const token = tokens().staffA;
  await page.addInitScript(fixture => { Object.assign(window, { __CRM_TEST_AUTH: fixture }); }, { token, allowed: true });
  await use(page);
} });
export { expect };
