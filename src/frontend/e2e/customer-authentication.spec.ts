import {test} from './fixtures/local-auth.fixture';
import {customerEmailJourney} from './fixtures/email-account-journeys';
test('public Customer signup stays signed out, then real signin/reload/signout and queue denial',async({page,context})=>customerEmailJourney({page,context},false));