import {test,expect} from './fixtures/local-auth.fixture';
import {restoreTicketIdentity,preserveTicketCookies} from './fixtures/ticket-local.fixture';
import {navigateTicket} from './fixtures/auth';

test.beforeEach(async({page})=>{
  await page.addInitScript(()=>{
    Object.defineProperty(navigator,'locks',{value:undefined,configurable:true});
    Object.defineProperty(window,'BroadcastChannel',{value:undefined,configurable:true});
  });
});

for(const direction of ['ltr','rtl'] as const){
  test(`auth forms and Admin pages remain usable in narrow ${direction} layouts`,async({page,context})=>{
    await page.goto('/auth/signin');
    await expect(page.getByRole('heading',{name:'Sign in',exact:true})).toBeVisible();
    await page.evaluate(dir=>document.documentElement.dir=dir,direction);
    for(const width of [320,375,768]){
      await page.setViewportSize({width,height:900});
      const email=page.getByLabel('Email',{exact:true});await email.focus();await expect(email).toBeFocused();
      await page.keyboard.press('Tab');await expect(page.getByLabel('Password',{exact:true})).toBeFocused();
      expect(await page.getByLabel('Password',{exact:true}).evaluate(input=>getComputedStyle(input).outlineStyle)).not.toBe('none');
      await expect(page.getByLabel('Password',{exact:true})).toHaveAttribute('type','password');
      expect(await page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
      await page.getByRole('link',{name:'Create Customer account',exact:true}).click();
      await expect(page.getByRole('heading',{name:'Create Customer account',exact:true})).toBeVisible();
      await expect(page.getByLabel('Display name',{exact:true})).toHaveClass(/ng-pristine/);
      await page.evaluate(()=>new Promise<void>(resolve=>requestAnimationFrame(()=>resolve())));
      await page.getByLabel('Display name',{exact:true}).fill('عميل عربي — English');
      await page.getByLabel('Password',{exact:true}).fill('  Unicode كلمة مرور  ');
      await expect(page.getByLabel('Display name',{exact:true})).toHaveValue('عميل عربي — English');
      await expect(page.getByLabel('Password',{exact:true})).toHaveValue('  Unicode كلمة مرور  ');
      await expect(page.getByLabel('Password',{exact:true})).toHaveAttribute('aria-describedby','password-policy');
      expect(await page.getByLabel('Password',{exact:true}).evaluate(input=>(input as HTMLInputElement).maxLength)).toBe(-1);
      expect(await page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
      await page.locator('main').getByRole('link',{name:'Sign in',exact:true}).click();
      await expect(page.getByRole('heading',{name:'Sign in',exact:true})).toBeVisible();
    }
    await restoreTicketIdentity(page,'admin');
    try{
      await page.evaluate(dir=>document.documentElement.dir=dir,direction);
      for(const width of [320,375,768]){
        await page.setViewportSize({width,height:900});
        await page.getByRole('navigation',{name:'Main',exact:true}).getByRole('link',{name:'Agents',exact:true}).click();
        await expect(page.getByRole('heading',{name:'Agents',exact:true})).toBeVisible();
        expect(await page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
        await page.getByRole('link',{name:'Create Agent',exact:true}).click();
        await expect(page.getByLabel('Password',{exact:true})).toBeVisible();
        expect(await page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
      }
      expect(await page.evaluate(()=>({local:localStorage.length,session:sessionStorage.length,readable:document.cookie.includes('__Host-crm-refresh')}))).toEqual({local:0,session:0,readable:false});
      const cookie=(await context.cookies()).find(cookie=>cookie.name==='__Host-crm-refresh')!;
      expect(cookie.httpOnly&&cookie.secure).toBe(true);expect(cookie.sameSite).toBe('Strict');
    }finally{await preserveTicketCookies(context,'admin');}
  });
}

for(const failure of ['service-unavailable','network'] as const){
  test(`concurrent same-instance requests clear authentication after ${failure} refresh without replay loops`,async({page,context})=>{
    await restoreTicketIdentity(page);
    try{
      let refreshes=0,rejectedReads=0;let releaseLate!:()=>void;
      const late=new Promise<void>(resolve=>releaseLate=resolve);
      // Transport failures are injected at HTTP boundaries; signin/cookies/state
      // remain real. The separate expiry test waits for the genuine five minutes.
      await page.route('**/api/auth/refresh',async route=>{
        refreshes++;if(failure==='network')await route.abort('failed');
        else await route.fulfill({status:503,contentType:'application/problem+json',body:JSON.stringify({code:'service_unavailable'})});
      });
      const reject=async(route:import('@playwright/test').Route)=>{
        expect(route.request().headers().authorization).toMatch(/^Bearer /);rejectedReads++;
        if(route.request().url().endsWith('/agents'))await late;
        await route.fulfill({status:401,contentType:'application/problem+json',body:JSON.stringify({code:'token_expired'})});
      };
      await page.route('**/api/ticket-reference-data/*',reject);
      await page.route('**/api/tickets?*',reject);
      await navigateTicket(page,'/tickets');
      await expect(page.getByRole('button',{name:'Sign out',exact:true})).toHaveCount(0);
      await expect(page.getByRole('link',{name:'Tickets',exact:true})).toHaveCount(0);
      await expect(page.getByRole('button',{name:'Retry tickets',exact:true})).toBeVisible();
      const expiredLate=page.waitForResponse(response=>response.url().endsWith('/api/ticket-reference-data/agents')&&response.status()===401);
      releaseLate();await expiredLate;
      await page.evaluate(()=>new Promise<void>(resolve=>requestAnimationFrame(()=>requestAnimationFrame(()=>resolve()))));
      expect(rejectedReads).toBe(3);expect(refreshes).toBe(1);
      await page.unroute('**/api/ticket-reference-data/*');await page.unroute('**/api/tickets?*');
      await page.getByRole('link',{name:'Account',exact:true}).click();
      await expect(page.getByRole('alert')).toContainText('No authenticated access is assumed.');
      expect(refreshes).toBe(1);
      expect(await page.evaluate(()=>({local:localStorage.length,session:sessionStorage.length}))).toEqual({local:0,session:0});
    }finally{await preserveTicketCookies(context);}
  });
}
