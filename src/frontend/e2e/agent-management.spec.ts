import {test} from './fixtures/local-auth.fixture';
import {agentEmailJourney} from './fixtures/email-account-journeys';
test('Admin creates renames disables and re-enables a real Agent; old authentication stays invalid',async({page,browser,context})=>agentEmailJourney({page,browser,context},false));