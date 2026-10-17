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

/** Matches Crm.Api CustomerResponse (own linked CRM profile). */
export interface MyCustomerProfile {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string | null;
  company: string | null;
  isRegistered: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface UpdateMyCustomerProfileRequest {
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
  company: string | null;
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

  getMyCustomerProfile(): Observable<MyCustomerProfile> {
    return this.http.get<MyCustomerProfile>(this.apiUrl.versioned('me/customer-profile'));
  }

  updateMyCustomerProfile(body: UpdateMyCustomerProfileRequest): Observable<MyCustomerProfile> {
    return this.http.put<MyCustomerProfile>(this.apiUrl.versioned('me/customer-profile'), body);
  }

  changePassword(body: ChangePasswordRequest): Observable<void> {
    return this.http.post<void>(this.apiUrl.versioned('me/password'), body);
  }
}
