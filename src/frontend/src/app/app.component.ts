import { Component, signal, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { Router } from '@angular/router';
import { AuthService } from './core/auth/auth.service';

@Component({
  imports: [RouterLink, RouterOutlet],
  selector: 'app-root',
  styleUrl: './app.component.css',
  templateUrl: './app.component.html',
})
export class App {
  protected readonly title = signal('crm');
  readonly auth=inject(AuthService);
  private readonly router=inject(Router);
  readonly authMessage=signal('');
  readonly signingOut=signal(false);
  async signOut(){if(this.signingOut())return;this.signingOut.set(true);this.authMessage.set('');try{await this.auth.signOut();await this.router.navigateByUrl('/auth/signin');}catch{this.authMessage.set('Signout could not be confirmed. Try again.');}finally{this.signingOut.set(false);}}
}
