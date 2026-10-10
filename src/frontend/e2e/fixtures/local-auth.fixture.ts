import { test as base, expect, Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
export type LocalRole='admin'|'agent'|'customer'|'disabled'|'staffA'|'staffB';
export interface LocalCredential {email:string;password:string;}
export const credentials=():Record<LocalRole,LocalCredential>=>{
  if(!process.env.CRM_AUTH_FIXTURE)throw new Error('Owned local-auth fixture required.');
  return JSON.parse(readFileSync(process.env.CRM_AUTH_FIXTURE,'utf8')).identities;
};
export async function signin(page:Page,role:LocalRole='admin') {
  await page.goto('/auth/signin');await expect(page.getByRole('heading',{name:'Sign in',exact:true})).toBeVisible();
  const account=credentials()[role];await page.getByLabel('Email',{exact:true}).fill(account.email);
  await page.getByLabel('Password',{exact:true}).evaluate((input,password)=>{(input as HTMLInputElement).value=password;input.dispatchEvent(new Event('input',{bubbles:true}));},account.password);
  // Observe the actual bearer published by the application; do not inject credentials.
  const identityRequest=role==='disabled'?null:page.waitForRequest(request=>request.url().endsWith('/api/auth/me')&&!!request.headers().authorization);
  const response=page.waitForResponse(response=>response.url().endsWith('/api/auth/signin'));
  await page.getByRole('button',{name:'Sign in',exact:true}).click();const result=await response;
  if(role==='disabled'){expect(result.status()).toBe(401);return null;}
  expect(result.status()).toBe(200);const authorization=(await identityRequest!).headers().authorization;expect(authorization.startsWith('Bearer ')).toBe(true);
  const accessToken=authorization.slice(7);const claims=JSON.parse(Buffer.from(accessToken.split('.')[1],'base64url').toString());
  const envelope={accessToken,tokenType:'Bearer',expiresAtUtc:new Date(claims.exp*1000).toISOString()};
  await expect(page).toHaveURL(role==='customer'?/\/access$/:/\/tickets\/new$/);return envelope;
}
export const test=base.extend({});
export {expect};
