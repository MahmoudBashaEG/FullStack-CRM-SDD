import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_CONFIG } from '../../core/api/api-config';
import { CustomerRecordsApi } from './customer-records-api';
import { CustomerAttachment } from './customer-records-models';

describe('CustomerRecordsApi', () => {
  it('uploads one file as multipart form data to the customer attachments endpoint and returns its metadata', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: '/api', version: 'v1' } },
      ],
    });
    const api = TestBed.inject(CustomerRecordsApi);
    const http = TestBed.inject(HttpTestingController);
    const file = new File(['%PDF-1.7'], 'warranty.pdf', { type: 'application/pdf' });
    let result: CustomerAttachment | undefined;

    api.uploadAttachment('c1', file).subscribe(attachment => (result = attachment));
    const request = http.expectOne('/api/v1/customers/c1/attachments');
    const form = request.request.body as FormData;
    expect(request.request.method).toBe('POST');
    expect(form instanceof FormData).toBe(true);
    expect(form.get('file')).toBeInstanceOf(File);
    expect((form.get('file') as File).name).toBe('warranty.pdf');
    expect(request.request.headers.has('Content-Type')).toBe(false);
    request.flush({
      id: 'a1', fileName: 'warranty.pdf', contentType: 'application/pdf', sizeBytes: 8,
      uploadedByUserId: 'u1', uploadedByName: 'Ahmed Agent', uploadedAtUtc: '2030-01-01T09:00:00Z',
    });

    expect(result?.fileName).toBe('warranty.pdf');
    expect(result?.sizeBytes).toBe(8);
    http.verify();
  });
});
