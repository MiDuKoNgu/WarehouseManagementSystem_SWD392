import { request } from '../../../shared/lib/http-client';
import type { AuditDetail, AuditOptions, AuditPage } from '../types/audit';

const base = '/api/admin/audit-logs';
export const auditApi = {
  options: () => request<AuditOptions>(`${base}/options`),
  list: (query: URLSearchParams) => request<AuditPage>(`${base}?${query}`),
  detail: (source: string, id: number) => request<AuditDetail>(`${base}/${encodeURIComponent(source)}/${id}`),
};
