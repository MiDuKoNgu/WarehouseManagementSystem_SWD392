import { ApiError, request } from '../../../shared/lib/http-client';
import type { CreatePurchaseOrder, ImportPreview, PurchaseOrderDetail, PurchaseOrderOptions, PurchaseOrderSummary } from '../types/purchase-order';

const base = '/api/purchase-orders';
async function post<T>(path: string, body: object | FormData, method = 'POST'): Promise<T> {
  const { token } = await request<{ token: string }>('/api/auth/csrf');
  const multipart = body instanceof FormData;
  return request<T>(path, {
    method,
    headers: { 'X-CSRF-TOKEN': token, ...(!multipart ? { 'Content-Type': 'application/json' } : {}) },
    body: multipart ? body : JSON.stringify(body),
  });
}
export const purchaseOrderApi = {
  options: () => request<PurchaseOrderOptions>(`${base}/options`),
  list: () => request<PurchaseOrderSummary[]>(base),
  detail: (id: number) => request<PurchaseOrderDetail>(`${base}/${id}`),
  create: (body: CreatePurchaseOrder) => post<PurchaseOrderDetail>(base, body),
  update: (id: number, body: CreatePurchaseOrder & { rowVersion: string }) => post<PurchaseOrderDetail>(`${base}/${id}`, body, 'PUT'),
  transition: (id: number, action: 'confirm' | 'cancel' | 'close', rowVersion: string, reason?: string) =>
    post<PurchaseOrderDetail>(`${base}/${id}/${action}`, { rowVersion, reason }),
  preview: (file: File) => {
    const form = new FormData(); form.append('file', file);
    return post<ImportPreview>(`${base}/import/preview`, form);
  },
  confirm: (token: string) => post<PurchaseOrderDetail>(`${base}/import/confirm`, { token }),
  template: async () => {
    let response: Response;
    try { response = await fetch(`${base}/import/template`, { credentials: 'same-origin', cache: 'no-store' }); }
    catch { throw new ApiError(0, 'Không thể kết nối máy chủ để tải mẫu Excel.'); }
    if (!response.ok) {
      const body = await response.json().catch(() => null);
      throw new ApiError(response.status, body?.message || 'Chưa thể tải mẫu Excel. Vui lòng thử lại.');
    }
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement('a'); link.href = url; link.download = 'EWMS-Purchase-Order-Template.xlsx';
    document.body.append(link); link.click(); link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  },
};
