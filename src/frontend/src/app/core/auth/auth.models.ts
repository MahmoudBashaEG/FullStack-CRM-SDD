export type CrmRole = 'Customer' | 'Agent' | 'Admin';
export interface CrmIdentity { userId: string; displayName: string; role: CrmRole; }
export interface TokenEnvelope { accessToken: string; tokenType: 'Bearer'; expiresAtUtc: string; }
export type AuthStatus = 'initializing' | 'authenticated' | 'anonymous' | 'unavailable';
