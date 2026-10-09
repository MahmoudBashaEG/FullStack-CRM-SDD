import { randomUUID } from 'node:crypto';
import { test,expect } from './fixtures/local-auth.fixture';
test('public Customer signup stays signed out, then real signin/reload/signout and queue denial',async({page,context})=>{
  const email=`customer-${randomUUID()}@fixture.test`,password='  عربي password  ';
  await page.goto('/auth/signup');await expect(page.getByRole('heading',{name:'Create Customer account'})).toBeVisible();
  await page.getByLabel('Display name',{exact:true}).fill('Customer عربي');await page.getByLabel('Email',{exact:true}).fill(email);
  await page.getByLabel('Password',{exact:true}).evaluate((input,value)=>{(input as HTMLInputElement).value=value;input.dispatchEvent(new Event('input',{bubbles:true}));},password);
  const created=page.waitForResponse(response=>response.url().endsWith('/api/auth/signup'));await page.getByRole('button',{name:'Create account',exact:true}).click();expect((await created).status()).toBe(201);
  await expect(page).toHaveURL(/\/auth\/signin$/);expect((await context.cookies()).some(cookie=>cookie.name==='__Host-crm-refresh')).toBe(false);await expect(page.getByRole('button',{name:'Sign out',exact:true})).toHaveCount(0);
  await page.getByLabel('Email',{exact:true}).fill(email);await page.getByLabel('Password',{exact:true}).evaluate((input,value)=>{(input as HTMLInputElement).value=value;input.dispatchEvent(new Event('input',{bubbles:true}));},password);
  const identity=page.waitForRequest(request=>request.url().endsWith('/api/auth/me')&&!!request.headers().authorization);await page.getByRole('button',{name:'Sign in',exact:true}).click();const token=(await identity).headers().authorization;
  await expect(page).toHaveURL(/\/access$/);await expect(page.getByText('Customer عربي (Customer)',{exact:true})).toBeVisible();
  const me=await context.request.get('/api/auth/me',{headers:{Authorization:token}});expect(me.status()).toBe(200);expect((await me.json()).role).toBe('Customer');
  expect((await context.request.get('/api/ticket-reference-data/categories',{headers:{Authorization:token}})).status()).toBe(403);
  await page.goto('/tickets');await expect(page).toHaveURL(/\/access$/);await page.reload();await expect(page.getByRole('button',{name:'Sign out',exact:true})).toBeVisible();
  const signedout=page.waitForResponse(response=>response.url().endsWith('/api/auth/signout'));await page.getByRole('button',{name:'Sign out',exact:true}).click();expect((await signedout).status()).toBe(204);
  expect((await context.request.get('/api/auth/me',{headers:{Authorization:token}})).status()).toBe(401);
});
