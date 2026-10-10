import { spawnSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { writeFileSync, unlinkSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import {updateAgentReference} from './ticket-local.fixture';

// Uses the existing operator CLI and the runner's isolated connection/Windows identity.
// No production reference administration route or fixture API is introduced.
export async function provisionReferences(input: {schemaVersion:number;categories?:object[];agents?:{id:string;displayName:string;isEligible:boolean;issuer?:string;subject?:string}[]}): Promise<void> {
  const root = resolve(process.cwd(), '../..');
  if (!process.env.CRM_AUTH_FIXTURE || process.env['ConnectionStrings__Crm'] !== process.env['CRM_E2E_SQL_CONNECTION']) throw new Error('Isolated E2E provisioning context required.');
  const file = join(dirname(process.env.CRM_AUTH_FIXTURE), `references-${randomUUID()}.json`);
  writeFileSync(file, JSON.stringify({schemaVersion:input.schemaVersion,categories:input.categories??[]}));
  try {
    const result = spawnSync('dotnet', [join(root, 'src/backend/Crm.Api/bin/Debug/net10.0/Crm.Api.dll'), 'provision-ticket-references', '--input', file], { cwd: root, env: process.env, encoding: 'utf8', shell: false });
    if (result.error || result.status !== 0) throw new Error('Synthetic reference provisioning failed.');
  } finally { unlinkSync(file); }
  for(const agent of input.agents??[])await updateAgentReference(agent.id,agent.displayName,agent.isEligible);
}
