import { spawnSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { writeFileSync, unlinkSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';

// Uses the existing operator CLI and the runner's isolated connection/Windows identity.
// No production reference administration route or fixture API is introduced.
export function provisionReferences(input: object): void {
  const root = resolve(process.cwd(), '../..');
  if (!process.env['CRM_E2E_TOKEN_FILE'] || process.env['ConnectionStrings__Crm'] !== process.env['CRM_E2E_SQL_CONNECTION']) throw new Error('Isolated E2E provisioning context required.');
  const file = join(dirname(process.env['CRM_E2E_TOKEN_FILE']), `references-${randomUUID()}.json`);
  writeFileSync(file, JSON.stringify(input));
  try {
    const result = spawnSync('dotnet', [join(root, 'src/backend/Crm.Api/bin/Debug/net10.0/Crm.Api.dll'), 'provision-ticket-references', '--input', file], { cwd: root, env: process.env, encoding: 'utf8', shell: false });
    if (result.error || result.status !== 0) throw new Error('Synthetic reference provisioning failed.');
  } finally { unlinkSync(file); }
}
