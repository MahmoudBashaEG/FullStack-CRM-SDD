export type UserRole = 'SuperAdmin' | 'Agent' | 'Customer';

/** Matches Crm.Api CurrentUserResponse. */
export interface CurrentUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
  role: UserRole;
}

/** Matches Crm.Api AuthSessionResponse. The refresh token is never visible to JS (HttpOnly cookie). */
export interface AuthSessionResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  user: CurrentUser;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterCustomerRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  phoneNumber: string | null;
}
