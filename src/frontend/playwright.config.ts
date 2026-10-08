import { defineConfig } from '@playwright/test';
import { resolve } from 'node:path';

export default defineConfig({
  testDir: './e2e/tickets', workers: 1, retries: 0,
  use: { baseURL: 'https://localhost:4200', ignoreHTTPSErrors: true, trace: 'off', screenshot: 'off', video: 'off' },
  webServer: [
    { command: 'dotnet run --no-build --no-launch-profile --project src/backend/Crm.Api', cwd: resolve('../..'),
      url: 'https://localhost:7043/api/ticket-reference-data/categories', reuseExistingServer: false, timeout: 120_000, ignoreHTTPSErrors: true },
    { command: 'npm run start:e2e', cwd: process.cwd(), url: 'https://localhost:4200', reuseExistingServer: false, timeout: 120_000, ignoreHTTPSErrors: true },
  ],
});
