import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
const root = fileURLToPath(new URL('../../', import.meta.url));
const proxy = JSON.parse(readFileSync(root + 'src/frontend/proxy.development.json'));
const workspace = JSON.parse(readFileSync(root + 'src/frontend/angular.json'));
assert.deepEqual(proxy, { '/api/**': { target: 'https://localhost:7043', secure: true } });
const serve = workspace.projects.crm.architect.serve.configurations;
assert.equal(serve.development.proxyConfig, 'proxy.development.json');
assert.equal(serve.e2e.proxyConfig, 'proxy.development.json');
assert.equal(serve.production.proxyConfig, undefined);
assert.match(readFileSync(root + 'src/frontend/src/environments/environment.ts', 'utf8'), /apiBaseUrl: '\/api'/);
// Observable nested routing, headers and TLS rejection run against real Angular in the integrated harness.
const { verifyProxy } = await import('./fixtures/proxy-observations.mjs');
if (!process.argv.includes('--configuration-only')) await verifyProxy(root);
console.log('PASS: development proxy contract' + (process.argv.includes('--configuration-only') ? ' (configuration only)' : ' and real forwarding/TLS observations'));
