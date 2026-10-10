import {test} from './fixtures/local-auth.fixture';
import {customerEmailJourney,agentEmailJourney} from './fixtures/email-account-journeys';
// Keep extra real-signin journeys after the established suite; do not relax rate limits.
test.beforeAll(async()=>{
  test.setTimeout(75000);
  // The established journeys consume the real per-IP ten-signin/minute budget.
  // Wait a real window before adding account journeys; no clock/rate-limit bypass.
  await new Promise(resolve=>setTimeout(resolve,61000));
});
test('NEL-wrapped quoted Customer signup, explicit signin, reload and role denial',async({page,context})=>customerEmailJourney({page,context},true,'\u0085'));
test('NEL-wrapped quoted Agent creation, assignment identity and disable/re-enable',async({page,browser,context})=>agentEmailJourney({page,browser,context},true,'\u0085'));
