import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import {
  AutomationRule,
  AutomationRuleRequest,
  BusinessCalendar,
  SlaDuePreview,
  SlaOverview,
  SlaPolicy,
  UpdateBusinessCalendarRequest,
  UpdateSlaPolicyRequest,
} from './sla-models';

/** SuperAdmin-only endpoints (policy, calendar, preview, overview, automation rules). */
@Injectable({ providedIn: 'root' })
export class SlaApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  getPolicy(): Observable<SlaPolicy> {
    return this.http.get<SlaPolicy>(this.apiUrl.versioned('sla/policy'));
  }

  savePolicy(body: UpdateSlaPolicyRequest): Observable<SlaPolicy> {
    return this.http.put<SlaPolicy>(this.apiUrl.versioned('sla/policy'), body);
  }

  getCalendar(): Observable<BusinessCalendar> {
    return this.http.get<BusinessCalendar>(this.apiUrl.versioned('sla/calendar'));
  }

  saveCalendar(body: UpdateBusinessCalendarRequest): Observable<BusinessCalendar> {
    return this.http.put<BusinessCalendar>(this.apiUrl.versioned('sla/calendar'), body);
  }

  /** startUtc null = the server's now. */
  previewDue(startUtc: string | null, minutes: number): Observable<SlaDuePreview> {
    let params = new HttpParams().set('minutes', minutes);
    if (startUtc) {
      params = params.set('startUtc', startUtc);
    }
    return this.http.get<SlaDuePreview>(this.apiUrl.versioned('sla/calendar/preview'), { params });
  }

  getOverview(): Observable<SlaOverview> {
    return this.http.get<SlaOverview>(this.apiUrl.versioned('sla/overview'));
  }

  listRules(): Observable<AutomationRule[]> {
    return this.http.get<AutomationRule[]>(this.apiUrl.versioned('automation-rules'));
  }

  getRule(id: string): Observable<AutomationRule> {
    return this.http.get<AutomationRule>(this.apiUrl.versioned(`automation-rules/${id}`));
  }

  createRule(body: AutomationRuleRequest): Observable<AutomationRule> {
    return this.http.post<AutomationRule>(this.apiUrl.versioned('automation-rules'), body);
  }

  updateRule(id: string, body: AutomationRuleRequest): Observable<AutomationRule> {
    return this.http.put<AutomationRule>(this.apiUrl.versioned(`automation-rules/${id}`), body);
  }
}
