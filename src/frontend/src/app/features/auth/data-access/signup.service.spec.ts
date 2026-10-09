import { describe,expect,it,vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { SignupService } from './signup.service';
describe('signup data access',()=>{
  it('delegates exact account input to the shared security transport without feature-owned authentication',async()=>{
    const registerCustomer=vi.fn(async():Promise<void>=>undefined);const service=new SignupService({registerCustomer} as unknown as AuthService);
    await service.register('Customer','customer@fixture.test','  عربي password  ');expect(registerCustomer).toHaveBeenCalledWith('Customer','customer@fixture.test','  عربي password  ');
  });
});
