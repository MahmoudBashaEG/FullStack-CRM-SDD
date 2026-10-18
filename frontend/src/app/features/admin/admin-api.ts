import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import { PagedResponse } from '../customers/customer-models';
import {
  AccountListParams,
  AccountStatusResponse,
  AdminAccount,
  AuditListParams,
  AuditLogEntry,
  OrganizationSettings,
  UpdateOrganizationSettingsRequest,
} from './admin-models';

/** SuperAdmin-only ADMIN-001 endpoints. Empty filters are omitted; the server derives the actor from the token. */
@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  listUsers({ page, pageSize, search, role, status }: AccountListParams): Observable<PagedResponse<AdminAccount>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    const term = search.trim();
    if (term) {
      params = params.set('search', term);
    }
    if (role) {
      params = params.set('role', role);
    }
    if (status) {
      params = params.set('isActive', status === 'active' ? 'true' : 'false');
    }
    return this.http.get<PagedResponse<AdminAccount>>(this.apiUrl.versioned('admin/users'), { params });
  }

  getUser(id: string): Observable<AdminAccount> {
    return this.http.get<AdminAccount>(this.apiUrl.versioned(`admin/users/${id}`));
  }

  setUserStatus(id: string, isActive: boolean): Observable<AccountStatusResponse> {
    return this.http.put<AccountStatusResponse>(this.apiUrl.versioned(`admin/users/${id}/status`), { isActive });
  }

  listAuditLogs({ page, pageSize, action, from, to, search }: AuditListParams): Observable<PagedResponse<AuditLogEntry>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    const term = search.trim();
    if (action) {
      params = params.set('action', action);
    }
    if (from) {
      params = params.set('from', from);
    }
    if (to) {
      params = params.set('to', to);
    }
    if (term) {
      params = params.set('search', term);
    }
    return this.http.get<PagedResponse<AuditLogEntry>>(this.apiUrl.versioned('admin/audit-logs'), { params });
  }

  getSettings(): Observable<OrganizationSettings> {
    return this.http.get<OrganizationSettings>(this.apiUrl.versioned('admin/organization-settings'));
  }

  updateSettings(version: string, body: UpdateOrganizationSettingsRequest): Observable<OrganizationSettings> {
    return this.http.put<OrganizationSettings>(this.apiUrl.versioned('admin/organization-settings'), body, {
      headers: { 'If-Match': `"${version}"` },
    });
  }
}
