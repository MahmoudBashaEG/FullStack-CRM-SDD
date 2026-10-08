import { spawnSync } from 'node:child_process';
import { generateKeyPairSync, sign, randomUUID } from 'node:crypto';
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, join, sep } from 'node:path';
import { createServer } from 'node:net';

const root = resolve(fileURLToPath(new URL('../../../', import.meta.url)));
const frontend = join(root, 'src/frontend');
const env = { ...process.env };
if (!env.CRM_E2E_SQL_CONNECTION) throw new Error('CRM_E2E_SQL_CONNECTION is required.');
env.ConnectionStrings__Crm = env.CRM_E2E_SQL_CONNECTION;
env.DOTNET_ENVIRONMENT = env.ASPNETCORE_ENVIRONMENT = 'Testing';
env.ASPNETCORE_URLS = 'https://localhost:7043';
env.Authentication__TestIssuer = 'https://example.test';
env.Authentication__TestAudience = 'crm-tickets-e2e';
const owner = randomUUID().replaceAll('-', '');
const directory = mkdtempSync(join(root, '.local/e2e-'));
if (!resolve(directory).startsWith(resolve(root, '.local') + sep)) throw new Error('Unsafe temporary fixture directory.');
const run = (command, args, cwd = root) => {
  const result = spawnSync(command, args, { cwd, env, stdio: 'inherit', shell: false });
  if (result.error || result.status !== 0) throw new Error('Test child process failed.');
};
const checkPort = port => new Promise((accept, reject) => {
  const server = createServer(); server.once('error', () => reject(new Error('Required test port is occupied.')));
  server.listen(port, 'localhost', () => server.close(accept));
});
let prepared = false;
let exitCode = 0;
try {
  await Promise.all([checkPort(7043), checkPort(4200)]);
  const username = spawnSync('whoami', [], { encoding: 'utf8' }).stdout.trim();
  run('icacls', [directory, '/inheritance:r', '/grant:r', `${username}:(OI)(CI)F`]);
  const { publicKey, privateKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
  env.Authentication__TestPublicKeyPath = join(directory, 'public.pem');
  writeFileSync(env.Authentication__TestPublicKeyPath, publicKey.export({ type: 'spki', format: 'pem' }));
  const token = (subject, allowed) => {
    const now = Math.floor(Date.now() / 1000);
    const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
    const unsigned = `${encode({ alg: 'RS256', typ: 'JWT' })}.${encode({ iss: env.Authentication__TestIssuer, aud: env.Authentication__TestAudience, sub: subject, name: 'Test Staff', iat: now, nbf: now - 5, exp: now + 1800, ...(allowed ? { crm_support_queue: 'shared' } : {}) })}`;
    return `${unsigned}.${sign('RSA-SHA256', Buffer.from(unsigned), privateKey).toString('base64url')}`;
  };
  env.CRM_E2E_TOKEN_FILE = join(directory, 'tokens.json');
  writeFileSync(env.CRM_E2E_TOKEN_FILE, JSON.stringify({ staffA: token('staff-a', true), staffB: token('staff-b', true), denied: token('staff-denied', false) }));
  run('pwsh', ['-NoProfile', '-File', 'scripts/e2e-database.ps1', '-Mode', 'Prepare', '-Owner', owner]);
  prepared = true;
  run('dotnet', ['ef', 'database', 'update', '--no-build', '--project', 'src/backend/Crm.Infrastructure', '--startup-project', 'src/backend/Crm.Api']);
  const input = join(directory, 'references.json');
  writeFileSync(input, JSON.stringify({ schemaVersion: 1, categories: [
    { id: '30000000-0000-0000-0000-000000000001', displayName: 'Test Category', isAvailable: true },
    { id: '30000000-0000-0000-0000-000000000002', displayName: 'Inactive Category', isAvailable: false }], agents: [
    { id: '20000000-0000-0000-0000-000000000001', issuer: 'https://example.test', subject: 'agent-a', displayName: 'Test Agent', isEligible: true },
    { id: '20000000-0000-0000-0000-000000000002', issuer: 'https://example.test', subject: 'agent-b', displayName: 'Inactive Agent', isEligible: false }] }));
  run('dotnet', ['run', '--no-build', '--no-launch-profile', '--project', 'src/backend/Crm.Api', '--', 'provision-ticket-references', '--input', input]);
  run(process.execPath, ['node_modules/@playwright/test/cli.js', 'test'], frontend);
} catch (error) { console.error(error.message); exitCode = 1; }
finally {
  try { if (prepared) run('pwsh', ['-NoProfile', '-File', 'scripts/e2e-database.ps1', '-Mode', 'Cleanup', '-Owner', owner]); }
  catch (error) { console.error(error.message); exitCode = 1; }
  rmSync(directory, { recursive: true, force: true });
}
process.exitCode = exitCode;
