import { test as base, expect, Page } from '@playwright/test';
import { restoreTicketIdentity,preserveTicketCookies } from './ticket-local.fixture';
export {tokens,actorId,fixtureReference} from './ticket-local.fixture';

// Ordinary Ticket navigation stays in the signed-in Angular instance. Explicit
// reload scenarios below still exercise the real rotating-cookie restoration.
export async function navigateTicket(page:Page,path:string):Promise<void>{
  const target=new URL(path,'https://localhost:4200').href;
  if(page.url()===target){await page.reload();return;}
  await page.evaluate(url=>{history.pushState({},'',url);window.dispatchEvent(new PopStateEvent('popstate',{state:{}}));},target);
  await expect(page).toHaveURL(target);
}

export const test = base.extend({ page: async ({ page }, use) => {
  await restoreTicketIdentity(page);
  try{await use(page);}finally{await preserveTicketCookies(page.context());}
} });
export { expect };
