import { ApiError, request } from '../../shared/lib/http-client';
import type { ReceiptDetail, ReceiptListItem, ReceiptOrder, ReceiptPolicy, ReceiptProgress, SaveReceiptItem } from './types';
const base = '/api/inbounds';
async function mutate<T>(path: string, body: unknown, method = 'POST'): Promise<T> {
  const { token } = await request<{ token: string }>('/api/auth/csrf');
  return request<T>(path, { method, headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token }, body: JSON.stringify(body), timeoutMs: 60000 });
}
export const inboundApi = {
  list: () => request<ReceiptListItem[]>(base),
  orders: () => request<ReceiptOrder[]>(`${base}/orders`),
  order: (id: number) => request<{ order: ReceiptOrder; lines: ReceiptProgress[]; policy: ReceiptPolicy }>(`${base}/orders/${id}`),
  detail: (id: number) => request<ReceiptDetail>(`${base}/${id}`),
  create: async (purchaseOrderId: number, note: string) => {
    const key = `ewms-inbound-create-${purchaseOrderId}`;
    let token = sessionStorage.getItem(key);
    if (!token) { token = crypto.randomUUID(); sessionStorage.setItem(key, token); }
    try {
      const value = await mutate<ReceiptDetail>(base, { purchaseOrderId, note, token });
      sessionStorage.removeItem(key);
      return value;
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) sessionStorage.removeItem(key);
      throw error;
    }
  },
  saveItem: (id: number, itemId: number | null, body: SaveReceiptItem) => mutate<ReceiptDetail>(`${base}/${id}/items${itemId === null ? '' : `/${itemId}`}`, body, itemId === null ? 'POST' : 'PUT'),
  removeItem: (id: number, itemId: number, rowVersion: string) => mutate<ReceiptDetail>(`${base}/${id}/items/${itemId}`, { rowVersion }, 'DELETE'),
  transition: (id: number, action: 'complete' | 'cancel', rowVersion: string, reason?: string) => mutate<ReceiptDetail>(`${base}/${id}/${action}`, { rowVersion, reason }),
};
