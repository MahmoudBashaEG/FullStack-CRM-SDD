import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {readFileSync,writeFileSync} from 'node:fs';
const {chromium,expect}=await import(new URL('../../../src/frontend/node_modules/@playwright/test/index.mjs',import.meta.url));
const manifestPath=process.env.CRM_AUTH_FIXTURE;
const setup=JSON.parse(readFileSync(manifestPath,'utf8'));
assert.match(setup.owner,/^[a-f0-9]{32}$/);
const origin='https://localhost:4200';
const browser=await chromium.launch({headless:true});
const contexts=[];
const context=async options=>{const result=await browser.newContext({baseURL:origin,ignoreHTTPSErrors:false,...options});contexts.push(result);return result;};
const signin=async(page,credential,role)=>{
  await page.goto('/auth/signin');await expect(page.getByRole('heading',{name:'Sign in',exact:true})).toBeVisible();
  await page.getByLabel('Email',{exact:true}).fill(credential.email);
  await page.getByLabel('Password',{exact:true}).evaluate((input,value)=>{input.value=value;input.dispatchEvent(new Event('input',{bubbles:true}));},credential.password);
  const me=page.waitForRequest(request=>request.url().endsWith('/api/auth/me')&&!!request.headers().authorization);
  const result=page.waitForResponse(response=>response.url().endsWith('/api/auth/signin'));
  await page.getByRole('button',{name:'Sign in',exact:true}).click();assert.equal((await result).status(),200);
  await expect(page).toHaveURL(role==='Customer'?/\/access$/:/\/tickets\/new$/);
  return(await me).headers().authorization;
};
const reload=async(page,jar,heading)=>{
  const before=(await jar.cookies()).find(cookie=>cookie.name==='__Host-crm-refresh');assert.ok(before.secure&&before.httpOnly&&before.sameSite==='Strict');
  await page.reload();await expect(page.getByRole('heading',{name:heading,exact:true})).toBeVisible();
  assert.ok((await jar.cookies()).find(cookie=>cookie.name==='__Host-crm-refresh').value!==before.value);
  assert.deepEqual(await page.evaluate(()=>({local:localStorage.length,session:sessionStorage.length,readable:document.cookie.includes('__Host-crm-refresh')})),{local:0,session:0,readable:false});
};
const signout=async(page,jar,authorization)=>{
  const result=page.waitForResponse(response=>response.url().endsWith('/api/auth/signout'));
  await page.getByRole('button',{name:'Sign out',exact:true}).click();assert.equal((await result).status(),204);
  assert.equal((await jar.request.get('/api/auth/me',{headers:{Authorization:authorization}})).status(),401);
};
try{
  if(process.argv.includes('--verify-restart')){
    const state=setup.developmentRestartState;assert.ok(state);
    const jar=await context({storageState:state.storageState});
    const response=await jar.request.post('/api/auth/refresh',{headers:{Origin:origin,'X-CSRF-TOKEN':state.csrf},data:{}});
    assert.equal(response.status(),200); // Uses the pre-restart request-token/cookie pair.
    const access=(await response.json()).accessToken;
    assert.equal((await jar.request.get('/api/auth/me',{headers:{Authorization:`Bearer ${access}`}})).status(),200);
    const page=await jar.newPage();await page.goto(`/tickets/${state.ticketReference}`);
    await expect(page.getByRole('heading',{level:1})).toHaveText(state.ticketReference);
    const after=await jar.request.get(`/api/tickets/${state.ticketReference}/history`,{headers:{Authorization:`Bearer ${access}`}});
    assert.deepEqual((await after.json()).items,state.history);
    await signout(page,jar,`Bearer ${access}`);
    console.log('PASS Development restart: retained CSRF/refresh pair, normal browser restore, unchanged Ticket history and signout.');
  }else{
    const adminContext=await context(),adminPage=await adminContext.newPage();
    const admin=await signin(adminPage,setup.identities.admin,'Admin');
    await reload(adminPage,adminContext,'Create ticket');
    await adminPage.getByRole('link',{name:'Agents',exact:true}).click();await adminPage.getByRole('link',{name:'Create Agent',exact:true}).click();
    const credential={email:`dev-agent-${randomUUID()}@owned-fixture.test`,password:`  كلمة مرور ${randomUUID()}  `};
    await adminPage.getByLabel('Display name',{exact:true}).fill('Agent عربي');await adminPage.getByLabel('Email',{exact:true}).fill(credential.email);
    await adminPage.getByLabel('Password',{exact:true}).evaluate((input,value)=>{input.value=value;input.dispatchEvent(new Event('input',{bubbles:true}));},credential.password);
    const creation=adminPage.waitForResponse(response=>response.url().endsWith('/api/agents')&&response.request().method()==='POST');
    await adminPage.getByRole('button',{name:'Create Agent',exact:true}).click();const created=await creation;assert.equal(created.status(),201);const agent=await created.json();
    await expect(adminPage.getByLabel('Agent active',{exact:true})).toBeVisible();
    const agentContext=await context(),agentPage=await agentContext.newPage();let agentToken=await signin(agentPage,credential,'Agent');
    await agentPage.getByLabel('Subject',{exact:true}).fill('Development سجل دعم');await agentPage.getByLabel('Description',{exact:true}).fill('Arabic / English support');
    const saved=agentPage.waitForResponse(response=>response.url().endsWith('/api/tickets')&&response.request().method()==='POST');
    await agentPage.getByRole('button',{name:'Create ticket',exact:true}).click();const ticketResponse=await saved;assert.equal(ticketResponse.status(),201);let ticket=await ticketResponse.json();
    await expect(agentPage.getByRole('heading',{level:1})).toHaveText(ticket.reference);
    await agentPage.getByLabel('Assigned agent',{exact:true}).selectOption(agent.assignmentReferenceId);
    const assignment=agentPage.waitForResponse(response=>response.url().endsWith('/assignment')&&response.request().method()==='PUT');
    await agentPage.getByRole('button',{name:'Save assignment',exact:true}).click();assert.equal((await assignment).status(),200);
    const headers=()=>({Authorization:agentToken});
    const historyBefore=(await(await agentContext.request.get(`/api/tickets/${ticket.reference}/history`,{headers:headers()})).json()).items;
    assert.equal(historyBefore[0].actor.subject,agent.userId);assert.equal(historyBefore[0].actor.displayName,'Agent عربي');
    await adminPage.getByLabel('Display name',{exact:true}).fill('Renamed Agent عربي');await adminPage.getByRole('button',{name:'Save name',exact:true}).click();await expect(adminPage.getByText('Saved.',{exact:true})).toBeVisible();
    await reload(agentPage,agentContext,ticket.reference);
    const current=(await(await agentContext.request.get('/api/auth/me',{headers:headers()})).json());assert.equal(current.displayName,'Renamed Agent عربي');
    ticket=await(await agentContext.request.get(`/api/tickets/${ticket.reference}`,{headers:headers()})).json();
    const edit=await agentContext.request.patch(`/api/tickets/${ticket.reference}/details`,{headers:headers(),data:{expectedVersion:ticket.editVersion,subject:'Renamed current actor'}});assert.equal(edit.status(),200);ticket=await edit.json();
    const history=(await(await agentContext.request.get(`/api/tickets/${ticket.reference}/history`,{headers:headers()})).json()).items;
    assert.deepEqual(history.slice(0,historyBefore.length),historyBefore);assert.equal(history.at(-1).actor.displayName,'Renamed Agent عربي');
    assert.equal((await agentContext.request.get('/api/agents',{headers:headers()})).status(),403);
    await adminPage.getByLabel('Agent active',{exact:true}).uncheck();await adminPage.getByRole('button',{name:'Save availability',exact:true}).click();await expect(adminPage.getByText('Disabled',{exact:true})).toBeVisible();
    assert.ok(JSON.parse(Buffer.from(agentToken.slice(7).split('.')[1],'base64url')).exp*1000>Date.now());
    assert.equal((await agentContext.request.get(`/api/tickets/${ticket.reference}`,{headers:headers()})).status(),401);
    const failedRefresh=agentPage.waitForResponse(response=>response.url().endsWith('/api/auth/refresh'));await agentPage.reload();assert.equal((await failedRefresh).status(),401);await expect(agentPage.getByRole('button',{name:'Sign out',exact:true})).toHaveCount(0);
    const options=await adminContext.request.get('/api/ticket-reference-data/agents',{headers:{Authorization:admin}});assert.ok(!(await options.json()).some(choice=>choice.id===agent.assignmentReferenceId));
    const retained=await adminContext.request.get(`/api/tickets/${ticket.reference}`,{headers:{Authorization:admin}});assert.equal((await retained.json()).assignedAgent.id,agent.assignmentReferenceId);
    await adminPage.getByLabel('Agent active',{exact:true}).check();await adminPage.getByRole('button',{name:'Save availability',exact:true}).click();await expect(adminPage.getByText('Active',{exact:true})).toBeVisible();
    assert.equal((await agentContext.request.get('/api/auth/me',{headers:headers()})).status(),401);
    agentToken=await signin(agentPage,credential,'Agent');await reload(agentPage,agentContext,'Create ticket');
    const restored=await agentContext.request.get('/api/ticket-reference-data/agents',{headers:headers()});assert.ok((await restored.json()).some(choice=>choice.id===agent.assignmentReferenceId));
    const customerContext=await context(),customerPage=await customerContext.newPage();const customer={email:`dev-customer-${randomUUID()}@owned-fixture.test`,password:`  عميل ${randomUUID()}  `};
    await customerPage.goto('/auth/signup');await expect(customerPage.getByLabel('Display name',{exact:true})).toHaveClass(/ng-pristine/);await customerPage.evaluate(()=>new Promise(resolve=>requestAnimationFrame(resolve)));
    await customerPage.getByLabel('Display name',{exact:true}).fill('Customer عربي');await customerPage.getByLabel('Email',{exact:true}).fill(customer.email);
    await customerPage.getByLabel('Password',{exact:true}).evaluate((input,value)=>{input.value=value;input.dispatchEvent(new Event('input',{bubbles:true}));},customer.password);
    const signup=customerPage.waitForResponse(response=>response.url().endsWith('/api/auth/signup'));await customerPage.getByRole('button',{name:'Create account',exact:true}).click();assert.equal((await signup).status(),201);
    await expect(customerPage).toHaveURL(/\/auth\/signin$/);assert.ok(!(await customerContext.cookies()).some(cookie=>cookie.name==='__Host-crm-refresh'));
    const customerToken=await signin(customerPage,customer,'Customer');await reload(customerPage,customerContext,'Account access');
    for(const path of ['/api/tickets','/api/agents'])assert.equal((await customerContext.request.get(path,{headers:{Authorization:customerToken}})).status(),403);
    await customerPage.goto('/tickets');await expect(customerPage).toHaveURL(/\/access$/);await signout(customerPage,customerContext,customerToken);
    await signout(adminPage,adminContext,admin);
    assert.equal((await agentContext.request.get('/api/auth/me',{headers:headers()})).status(),200); // Independent session remains live.
    const csrf=await agentContext.request.get('/api/auth/csrf');assert.equal(csrf.status(),200);
    setup.developmentRestartState={storageState:await agentContext.storageState(),csrf:(await csrf.json()).requestToken,ticketReference:ticket.reference,history};
    writeFileSync(manifestPath,JSON.stringify(setup));
    console.log('PASS actual Development quickstart: bootstrap Admin, role UI/cookie flows, Customer denial, Agent rename/disable/re-enable, independent signout and immutable history.');
  }
}finally{for(const jar of contexts)await jar.close();await browser.close();}
