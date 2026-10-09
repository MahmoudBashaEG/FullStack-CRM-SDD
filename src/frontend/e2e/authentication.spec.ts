import {test,expect,signin,credentials} from './fixtures/local-auth.fixture';
import {spawnSync} from 'node:child_process';
import {resolve} from 'node:path';

test.beforeEach(async({page})=>{
  await page.addInitScript(()=>{Object.defineProperty(navigator,'locks',{value:undefined,configurable:true});Object.defineProperty(window,'BroadcastChannel',{value:undefined,configurable:true});});
});
test('real Admin signin, private cookies, reload restoration and signout revocation',async({page,context})=>{
  const token=(await signin(page))!;expect(token.tokenType).toBe('Bearer');
  const cookies=await context.cookies();const refresh=cookies.find(cookie=>cookie.name==='__Host-crm-refresh')!;expect(refresh.secure).toBe(true);expect(refresh.httpOnly).toBe(true);expect(refresh.sameSite).toBe('Strict');expect(refresh.path).toBe('/');
  expect(await page.evaluate(()=>({local:localStorage.length,session:sessionStorage.length,readable:document.cookie.includes('__Host-crm-refresh')}))).toEqual({local:0,session:0,readable:false});
  const me=await context.request.get('/api/auth/me',{headers:{Authorization:`Bearer ${token.accessToken}`}});expect(me.status()).toBe(200);const identity=await me.json();expect(Object.keys(identity).sort()).toEqual(['displayName','role','userId']);expect(identity.role).toBe('Admin');
  const before=(await context.cookies()).find(cookie=>cookie.name==='__Host-crm-refresh')!.value;await page.reload();await expect(page.getByRole('heading',{name:'Create ticket',exact:true})).toBeVisible();const after=(await context.cookies()).find(cookie=>cookie.name==='__Host-crm-refresh')!.value;expect(after!==before).toBe(true);
  await page.getByRole('button',{name:'Sign out',exact:true}).click();await expect(page).toHaveURL(/\/auth\/signin$/);
  expect((await context.request.get('/api/auth/me',{headers:{Authorization:`Bearer ${token.accessToken}`}})).status()).toBe(401);
  await page.reload();await page.goto('/tickets');await expect(page).toHaveURL(/\/auth\/signin$/);expect((await context.cookies()).some(cookie=>cookie.name==='__Host-crm-refresh')).toBe(false);
});
test('Customer and disabled credentials respect role and generic failure boundaries',async({page,context})=>{
  const token=(await signin(page,'customer'))!;await expect(page.getByText('customer (Customer)')).toBeVisible();await expect(page.getByRole('link',{name:'Tickets',exact:true})).toHaveCount(0);
  expect((await context.request.get('/api/ticket-reference-data/categories',{headers:{Authorization:`Bearer ${token.accessToken}`}})).status()).toBe(403);
  expect((await context.request.get('/api/auth/me',{headers:{Authorization:'Bearer malformed'}})).status()).toBe(401);
  await page.getByRole('button',{name:'Sign out',exact:true}).click();await expect(page).toHaveURL(/\/auth\/signin$/);await signin(page,'disabled');await expect(page.getByRole('alert')).toHaveText('Email or password is incorrect.');
  await page.getByLabel('Email',{exact:true}).fill('absent@owned-fixture.test');await page.getByLabel('Password',{exact:true}).fill('invalid but policy valid');await page.getByRole('button',{name:'Sign in',exact:true}).click();await expect(page.getByRole('alert')).toHaveText('Email or password is incorrect.');
});
test('signout leaves a separate browser context authenticated',async({page,browser})=>{
  await signin(page);const other=await browser.newContext({baseURL:'https://localhost:4200',ignoreHTTPSErrors:false});try{
    const second=await other.newPage();const token=(await signin(second,'agent'))!;await page.getByRole('button',{name:'Sign out',exact:true}).click();await expect(page).toHaveURL(/\/auth\/signin$/);
    expect((await other.request.get('/api/auth/me',{headers:{Authorization:`Bearer ${token.accessToken}`}})).status()).toBe(200);await second.reload();await expect(second.getByRole('heading',{name:'Create ticket',exact:true})).toBeVisible();
  }finally{await other.close();}
});
test('real five-minute expiry coalesces concurrent same-page protected requests',async({page})=>{
  test.setTimeout(360000);const envelope=(await signin(page,'agent'))!;
  const payload=JSON.parse(Buffer.from(envelope.accessToken.split('.')[1],'base64url').toString());expect(payload.exp-payload.iat).toBe(300);
  await page.getByRole('link',{name:'Account',exact:true}).click();await expect(page).toHaveURL(/\/access$/);
  let refreshes=0;let expired=0;page.on('request',request=>{if(request.url().endsWith('/api/auth/refresh'))refreshes++;});page.on('response',response=>{if(response.status()===401&&(response.url().includes('/api/tickets')||response.url().includes('/api/ticket-reference-data/')))expired++;});
  // Wait for the real server JWT lifetime; no shortened lifetime, forged token or clock bypass.
  await page.waitForTimeout(Math.max(0,Date.parse(envelope.expiresAtUtc)-Date.now()+1200));
  await page.getByRole('link',{name:'Tickets',exact:true}).click();await expect(page.getByText('No tickets match these filters.')).toBeVisible();await expect(page.getByRole('alert')).toHaveCount(0);expect(expired).toBeGreaterThanOrEqual(2);expect(refreshes).toBe(1);
});
test('strict local development proxy preserves auth transport and revocation',async()=>{
  const result=spawnSync(process.execPath,[resolve('../../tests/scripts/local-auth-proxy.tests.mjs')],{env:process.env,stdio:'inherit'});expect(result.status).toBe(0);
});
test('failed restoration clears browser access without a refresh loop',async({page})=>{
  await signin(page);let attempts=0;await page.route('**/api/auth/refresh',async route=>{attempts++;await route.fulfill({status:503,contentType:'application/problem+json',body:JSON.stringify({code:'service_unavailable'})});});
  await page.goto('/access');await expect(page.getByRole('alert')).toContainText('Authentication is unavailable.');await expect(page.getByRole('link',{name:'Tickets',exact:true})).toHaveCount(0);await expect(page.getByRole('button',{name:'Sign out',exact:true})).toHaveCount(0);expect(attempts).toBe(1);
});
test('malformed restoration fails closed without publishing an invalid bearer',async({page})=>{
  await signin(page);let identityRequests=0;page.on('request',request=>{if(request.url().endsWith('/api/auth/me'))identityRequests++;});
  await page.route('**/api/auth/refresh',route=>route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({accessToken:'',tokenType:'Bearer',expiresAtUtc:new Date(Date.now()+300000).toISOString()})}));
  await page.goto('/access');await expect(page.getByRole('alert')).toContainText('Authentication is unavailable.');expect(identityRequests).toBe(0);await expect(page.getByRole('link',{name:'Tickets',exact:true})).toHaveCount(0);
});
test('unconfirmed signout remains visible and can be explicitly retried',async({page,context})=>{
  const envelope=(await signin(page))!;await page.route('**/api/auth/signout',route=>route.fulfill({status:503,contentType:'application/problem+json',body:JSON.stringify({code:'service_unavailable'})}));
  await page.getByRole('button',{name:'Sign out',exact:true}).click();await expect(page.getByRole('alert')).toContainText('Signout could not be confirmed.');
  expect((await context.request.get('/api/auth/me',{headers:{Authorization:`Bearer ${envelope.accessToken}`}})).status()).toBe(200);
  await page.unroute('**/api/auth/signout');await page.getByRole('button',{name:'Retry signout',exact:true}).click();await expect(page).toHaveURL(/\/auth\/signin$/);
  expect((await context.request.get('/api/auth/me',{headers:{Authorization:`Bearer ${envelope.accessToken}`}})).status()).toBe(401);
});
