/** Matches Crm.Api CustomerResponse. */
export interface Customer {
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

/** Matches Crm.Api PagedResponse<T>. page is one-based. */
export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CustomerListParams {
  page: number;
  pageSize: number;
  search: string | null;
}

/** No password/role/userId: contact-only customers never get a login. */
export interface CreateCustomerRequest {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string | null;
  company: string | null;
}

/** For registered customers, email must be the unchanged login email. */
export type UpdateCustomerRequest = CreateCustomerRequest;
