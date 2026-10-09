import assert from 'node:assert/strict';
export async function verifyProxy() {
  if (process.argv.includes('--tls-negative')) {
    const rejected = await fetch('https://localhost:4200/api/ticket-reference-data/categories');
    assert.ok(rejected.status >= 500, 'An untrusted backend certificate must fail at the proxy, never return the fixture backend 401');
    console.log('PASS: actual Angular proxy rejected the backend certificate with no trusted CA');
    return;
  }
  const response = await fetch('https://localhost:4200/api/ticket-reference-data/categories');
  assert.equal(response.status, 401);
  const notApi = await fetch('https://localhost:4200/api-docs');
  assert.equal(notApi.status, 200);
  assert.match(await notApi.text(), /<html/i);
  // Bearer/path/query and trust-negative observations are driven by run-dev.integration.ps1.
}
