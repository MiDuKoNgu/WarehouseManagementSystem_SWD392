import { request } from '../../../shared/lib/http-client';
import type { ProductDetail, ProductInput, ProductOptions, ProductPage } from '../types/product';

async function save(body: ProductInput, id?: number) {
  const { token } = await request<{ token: string }>('/api/auth/csrf');
  return request<ProductDetail>(id ? `/api/products/${id}` : '/api/products', {
    method: id ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token }, body: JSON.stringify(body),
  });
}
export const productApi = {
  list: (query: string) => request<ProductPage>(`/api/products${query ? '?' + query : ''}`),
  options: () => request<ProductOptions>('/api/products/options'),
  detail: (id: number) => request<ProductDetail>(`/api/products/${id}`),
  save,
};
