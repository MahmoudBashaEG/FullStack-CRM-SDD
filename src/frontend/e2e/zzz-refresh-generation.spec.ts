import {test,expect,signin,credentials} from './fixtures/local-auth.fixture';

test.beforeAll(async()=>{
  test.setTimeout(75000);
  await new Promise(resolve=>setTimeout(resolve,61000));
});
for(const lateStatus of [200,503]) test(`invalidated refresh ${lateStatus} cannot overtake newer signin cookie`,async({page,context})=>{
  await signin(page,'agent');let release!:()=>void;let began!:()=>void;
  const held=new Promise<void>(resolve=>release=resolve);const started=new Promise<void>(resolve=>began=resolve);
  let delivered=false,signinBeforeDelivery=false,refreshes=0;
  await page.route('**/api/auth/refresh',async route=>{
    refreshes++;const response=await route.fetch();expect(response.status()).toBe(200);began();await held;
    // Rotation remains real. The failure case models failed delivery after commit.
    await route.fulfill(lateStatus===200?{response}:{response,status:503,contentType:'application/problem+json',body:JSON.stringify({code:'service_unavailable'})});
  });
  await page.route('**/api/ticket-reference-data/categories',route=>route.fulfill({status:401,contentType:'application/problem+json',body:JSON.stringify({code:'token_expired'})}));
  await page.route('**/api/ticket-reference-data/agents',route=>route.fulfill({status:200,contentType:'application/json',body:'[]'}));
  await page.route('**/api/tickets?*',async route=>{await started;await route.fulfill({status:401,contentType:'application/problem+json',body:JSON.stringify({code:'authentication_invalid'})});});
  page.on('response',response=>{if(response.url().endsWith('/api/auth/refresh'))delivered=true;});
  page.on('request',request=>{if(request.url().endsWith('/api/auth/signin')&&!delivered)signinBeforeDelivery=true;});
  try{
    await page.getByRole('link',{name:'Tickets',exact:true}).click();await started;
    await expect(page.getByRole('button',{name:'Sign out',exact:true})).toHaveCount(0);
    await page.getByRole('navigation',{name:'Main'}).getByRole('link',{name:'Sign in',exact:true}).click();
    await expect(page).toHaveURL(/\/auth\/signin$/);await expect(page.getByRole('heading',{name:'Sign in',exact:true})).toBeVisible();
    // SPA navigation renders the controls before NgModel's deferred registration.
    // Complete that render before input, keeping the old refresh held throughout.
    await page.evaluate(()=>new Promise<void>(resolve=>requestAnimationFrame(()=>requestAnimationFrame(()=>resolve()))));
    const account=credentials().customer;await page.getByLabel('Email',{exact:true}).fill(account.email);await page.getByLabel('Password',{exact:true}).evaluate((input,password)=>{(input as HTMLInputElement).value=password;input.dispatchEvent(new Event('input',{bubbles:true}));},account.password);
    await expect(page.getByLabel('Email',{exact:true})).toHaveValue(account.email);
    const signedin=page.waitForResponse(response=>response.url().endsWith('/api/auth/signin'));await page.getByRole('button',{name:'Sign in',exact:true}).click();
    // Let the Angular form handler/microtasks run while delivery is held.
    await page.evaluate(()=>new Promise<void>(resolve=>requestAnimationFrame(()=>requestAnimationFrame(()=>resolve()))));
    release();const response=await signedin;const submitted=response.request().postDataJSON();expect(submitted.email===account.email).toBe(true);expect(submitted.password===account.password).toBe(true);expect(response.status()).toBe(200);await expect(page).toHaveURL(/\/access$/);await expect(page.getByText('customer (Customer)',{exact:true})).toBeVisible();
    expect(signinBeforeDelivery).toBe(false);expect(refreshes).toBe(1);
    await page.unroute('**/api/auth/refresh');await page.reload();await expect(page.getByText('customer (Customer)',{exact:true})).toBeVisible();
    await expect(page.getByRole('link',{name:'Tickets',exact:true})).toHaveCount(0);
    const refresh=(await context.cookies()).find(cookie=>cookie.name==='__Host-crm-refresh')!;expect(refresh.secure&&refresh.httpOnly).toBe(true);
    expect(await page.evaluate(()=>({local:localStorage.length,session:sessionStorage.length}))).toEqual({local:0,session:0});
    await page.getByRole('button',{name:'Sign out',exact:true}).click();await expect(page).toHaveURL(/\/auth\/signin$/);await page.reload();await expect(page.getByRole('button',{name:'Sign out',exact:true})).toHaveCount(0);
  }finally{release();}
});
