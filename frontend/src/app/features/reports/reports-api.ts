import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import {
  AgentPerformanceReport,
  CustomerSatisfactionReport,
  ReportPeriodParams,
  SlaReport,
  TicketReport,
} from './reports-models';

/** REPORT-001: read-only GETs (SuperAdmin). The same four endpoints feed the report pages and the dashboard. */
@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  tickets(period: ReportPeriodParams): Observable<TicketReport> {
    return this.get('reports/tickets', period);
  }

  sla(period: ReportPeriodParams): Observable<SlaReport> {
    return this.get('reports/sla', period);
  }

  agents(period: ReportPeriodParams): Observable<AgentPerformanceReport> {
    return this.get('reports/agents', period);
  }

  customerSatisfaction(period: ReportPeriodParams): Observable<CustomerSatisfactionReport> {
    return this.get('reports/customer-satisfaction', period);
  }

  /** preset always; from/to only for Custom (the server ignores them otherwise). */
  private get<T>(path: string, { preset, from, to }: ReportPeriodParams): Observable<T> {
    let params = new HttpParams().set('preset', preset);
    if (preset === 'Custom' && from && to) {
      params = params.set('from', from).set('to', to);
    }
    return this.http.get<T>(this.apiUrl.versioned(path), { params });
  }
}
