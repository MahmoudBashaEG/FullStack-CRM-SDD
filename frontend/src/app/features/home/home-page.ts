import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { HealthApi, HealthResponse } from '../../core/api/health-api';
import { AuthService } from '../../core/auth/auth-service';
import { ProblemDetails } from '../../core/api/problem-details';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';

type ConnectionState =
  | { kind: 'checking' }
  | { kind: 'online'; health: HealthResponse }
  | { kind: 'offline'; message: string };

function describeError(error: HttpErrorResponse): string {
  if (error.status === 0) {
    return 'The API is not reachable. Start the backend and try again.';
  }
  const problem = error.error as ProblemDetails | null;
  return problem?.title ?? `The API responded with status ${error.status}.`;
}

@Component({
  selector: 'crm-home-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PageHeader, StatusBadge, MatCardModule, MatButtonModule, MatIconModule, DatePipe, RouterLink],
  templateUrl: './home-page.html',
  styleUrl: './home-page.scss',
})
export class HomePage {
  private readonly healthApi = inject(HealthApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly auth = inject(AuthService);
  protected readonly user = this.auth.currentUser;
  protected readonly isCustomer = computed(() => this.auth.role() === 'Customer');
  protected readonly connection = signal<ConnectionState>({ kind: 'checking' });

  constructor() {
    this.checkConnection();
  }

  protected checkConnection(): void {
    this.connection.set({ kind: 'checking' });
    this.healthApi.getHealth()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: health => this.connection.set({ kind: 'online', health }),
        error: (error: HttpErrorResponse) =>
          this.connection.set({ kind: 'offline', message: describeError(error) }),
      });
  }
}
