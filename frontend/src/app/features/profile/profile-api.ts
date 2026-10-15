import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import { CurrentUser } from '../../core/auth/auth-models';

export interface UpdateProfileRequest {
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

@Injectable({ providedIn: 'root' })
export class ProfileApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  getMe(): Observable<CurrentUser> {
    return this.http.get<CurrentUser>(this.apiUrl.versioned('me'));
  }

  updateMe(body: UpdateProfileRequest): Observable<CurrentUser> {
    return this.http.put<CurrentUser>(this.apiUrl.versioned('me'), body);
  }

  changePassword(body: ChangePasswordRequest): Observable<void> {
    return this.http.post<void>(this.apiUrl.versioned('me/password'), body);
  }
}
