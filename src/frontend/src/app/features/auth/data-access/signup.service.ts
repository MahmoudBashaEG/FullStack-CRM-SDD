import { Injectable } from '@angular/core';
import { AuthService } from '../../../core/auth/auth.service';

@Injectable({providedIn:'root'})
export class SignupService {
  constructor(private readonly auth:AuthService) {}
  register(displayName:string,email:string,password:string):Promise<void> {
    return this.auth.registerCustomer(displayName,email,password);
  }
}
