import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { App } from '../src/app/app';

const buyer = { userId: 2, username: 'buyer', fullName: 'Buyer', role: 'PURCHASING_STAFF', warehouseId: null };
const product = { productId: 1, productCode: 'TV-001', productName: 'Ti vi Samsung', model: 'TV-DEMO', unit: 'Chiếc', categoryId: 1, categoryName: 'Ti vi', brandId: 2, brandName: 'Samsung', status: 'ACTIVE', specifications: 'Thông số demo', lengthCm: 120, widthCm: 20, heightCm: 80, grossWeightKg: 15, suppliers: [{ supplierId: 1, supplierName: 'Nhà cung cấp Bắc' }], createdByUserId: 2, createdAt: '2026-11-01T00:00:00Z', updatedAt: null, rowVersion: 'AAAAAAAAAAE=' };
const options = { categories: [{ categoryId: 1, categoryName: 'Ti vi' }], brands: [{ brandId: 2, brandName: 'Samsung' }], suppliers: [{ supplierId: 1, supplierName: 'Nhà cung cấp Bắc' }] };
const order = { purchaseOrderId: 10, poCode: 'PO-TEST', supplierId: 1, supplierName: 'Nhà cung cấp Bắc', warehouseId: 1, warehouseName: 'Kho A', orderDate: '2026-11-01', expectedArrivalDate: null, status: 'DRAFT', createdAt: '2026-11-01T00:00:00Z', lineCount: 1, totalQuantity: 10, createdByUserId: 2, createdByName: 'Buyer', note: null, rowVersion: 'AAAAAAAAAAE=', confirmedAt: null, closedAt: null, closeReason: null, hasAnyInbound: false, openInboundCount: 0, lines: [{ poLineId: 1, productId: 1, productCode: 'TV-001', productName: 'Ti vi Samsung', unit: 'Chiếc', quantity: 10, receivedQuantity: 0, closedQuantity: 0, remainingQuantity: 10 }] };
let currentOrder = { ...order };
const response = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } });

beforeEach(() => {
  currentOrder = { ...order }; window.history.replaceState(null, '', '/purchasing/products');
  vi.stubGlobal('fetch', vi.fn(async (path: string, init?: RequestInit) => {
    if (path === '/api/auth/me') return response(buyer);
    if (path === '/api/auth/csrf') return response({ token: 'csrf-test' });
    if (path === '/api/products/options') return response(options);
    if (path.startsWith('/api/products?')) {
      const page = Number(new URL(path, 'http://localhost').searchParams.get('page') || 1);
      return response({ items: [product], totalCount: 21, page, pageSize: 20 });
    }
    if (path === '/api/products/1' && init?.method === 'PUT') return response({ ...product, ...JSON.parse(String(init.body)) });
    if (path === '/api/products' && init?.method === 'POST') return response(product, 201);
    if (path === '/api/products/1') return response(product);
    if (path === '/api/purchase-orders/10') return response(currentOrder);
    if (path === '/api/purchase-orders/10/confirm') { currentOrder = { ...currentOrder, status: 'CONFIRMED' }; return response(currentOrder); }
    if (path === '/api/purchase-orders/10/cancel') { currentOrder = { ...currentOrder, status: 'CANCELLED', closeReason: JSON.parse(String(init?.body)).reason }; return response(currentOrder); }
    if (path === '/api/purchase-orders/10/close') { currentOrder = { ...currentOrder, status: 'CLOSED', closeReason: JSON.parse(String(init?.body)).reason }; return response(currentOrder); }
    throw new Error(`Unexpected ${init?.method || 'GET'} ${path}`);
  }));
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('sends product search and filters to the server and preserves them while paging', async () => {
  render(<App />); await screen.findByText('TV-001');
  fireEvent.change(screen.getByLabelText('Tìm kiếm'), { target: { value: 'Ti vi' } });
  fireEvent.change(screen.getByLabelText('Danh mục'), { target: { value: '1' } });
  fireEvent.change(screen.getByLabelText('Hãng'), { target: { value: '2' } });
  fireEvent.change(screen.getByLabelText('Nhà cung cấp'), { target: { value: '1' } });
  fireEvent.change(screen.getByLabelText('Trạng thái'), { target: { value: 'ACTIVE' } });
  fireEvent.click(screen.getByRole('button', { name: 'Áp dụng bộ lọc' }));
  await waitFor(() => expect(window.location.search).toContain('search=Ti+vi'));
  await screen.findByText('TV-001');
  fireEvent.click(screen.getByRole('button', { name: 'Trang sau →' }));
  await waitFor(() => expect(window.location.search).toContain('page=2'));
  const calls = vi.mocked(fetch).mock.calls.filter(([path]) => String(path).startsWith('/api/products?'));
  const query = new URL(String(calls.at(-1)![0]), 'http://localhost').searchParams;
  expect(query.get('search')).toBe('Ti vi'); expect(query.get('categoryId')).toBe('1'); expect(query.get('supplierId')).toBe('1'); expect(query.get('status')).toBe('ACTIVE');
});

it('edits a product using the version token, preserves code and saves supplier associations', async () => {
  window.history.replaceState(null, '', '/purchasing/products/1/edit'); render(<App />);
  await screen.findByDisplayValue('TV-001');
  expect((screen.getByLabelText(/Mã sản phẩm/) as HTMLInputElement).readOnly).toBe(true);
  fireEvent.change(screen.getByLabelText('Tên sản phẩm'), { target: { value: 'Ti vi Samsung mới' } });
  fireEvent.click(screen.getByRole('button', { name: 'Lưu sản phẩm →' }));
  await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([path, init]) => path === '/api/products/1' && init?.method === 'PUT')).toBe(true));
  const [, request] = vi.mocked(fetch).mock.calls.find(([path, init]) => path === '/api/products/1' && init?.method === 'PUT')!;
  const body = JSON.parse(String(request?.body));
  expect(body.productCode).toBe('TV-001'); expect(body.rowVersion).toBe('AAAAAAAAAAE='); expect(body.supplierIds).toEqual([1]); expect(body.productName).toBe('Ti vi Samsung mới');
  expect((request?.headers as Record<string, string>)['X-CSRF-TOKEN']).toBe('csrf-test');
});

it('requires at least one supplier before creating a product', async () => {
  window.history.replaceState(null, '', '/purchasing/products/new'); render(<App />);
  await screen.findByRole('button', { name: 'Lưu sản phẩm →' });
  for (const [label, value] of [['Mã sản phẩm', 'NEW-001'], ['Tên sản phẩm', 'Thiết bị mới'], ['Model', 'DEMO'], ['Danh mục', '1'], ['Hãng', '2'], ['Chiều dài (cm)', '10'], ['Chiều rộng (cm)', '10'], ['Chiều cao (cm)', '10'], ['Khối lượng (kg)', '1']])
    fireEvent.change(screen.getByLabelText(label), { target: { value } });
  fireEvent.submit(screen.getByRole('button', { name: 'Lưu sản phẩm →' }).closest('form')!);
  await screen.findByRole('alert'); expect(screen.getByText('Vui lòng chọn ít nhất một nhà cung cấp.')).toBeTruthy();
  expect(vi.mocked(fetch).mock.calls.some(([path, init]) => path === '/api/products' && init?.method === 'POST')).toBe(false);
});

it('denies product management to non-purchasing users before product requests', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => response({ ...buyer, role: 'ADMIN' })));
  render(<App />); await screen.findByRole('heading', { name: 'Bạn đang ở nhầm không gian.' });
  expect(vi.mocked(fetch).mock.calls.every(([path]) => path === '/api/auth/me')).toBe(true);
});

it('confirms a draft PO using CSRF and its row version, then removes editing actions', async () => {
  window.history.replaceState(null, '', '/purchasing/purchase-orders/10'); render(<App />);
  await screen.findByRole('heading', { name: 'PO-TEST' });
  expect(screen.getByRole('link', { name: 'Chỉnh sửa bản nháp' })).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Xác nhận PO' }));
  const dialog = screen.getByRole('dialog');
  fireEvent.click(within(dialog).getByRole('button', { name: 'Xác nhận PO' }));
  await screen.findByText('Xác nhận PO thành công. Lịch sử thao tác đã được lưu.', { exact: false });
  expect(screen.queryByRole('link', { name: 'Chỉnh sửa bản nháp' })).toBeNull();
  const [, init] = vi.mocked(fetch).mock.calls.find(([path]) => path === '/api/purchase-orders/10/confirm')!;
  expect(JSON.parse(String(init?.body)).rowVersion).toBe('AAAAAAAAAAE=');
});

it('requires a cancel reason and sends it only after the user confirms the dialog', async () => {
  window.history.replaceState(null, '', '/purchasing/purchase-orders/10'); render(<App />);
  await screen.findByRole('heading', { name: 'PO-TEST' }); fireEvent.click(screen.getByRole('button', { name: 'Hủy PO' }));
  const dialog = screen.getByRole('dialog');
  fireEvent.submit(within(dialog).getByRole('button', { name: 'Hủy PO' }).closest('form')!);
  expect(within(dialog).getByText('Vui lòng nhập lý do.')).toBeTruthy();
  fireEvent.change(within(dialog).getByLabelText(/Lý do hủy/), { target: { value: 'Không còn nhu cầu' } });
  fireEvent.click(within(dialog).getByRole('button', { name: 'Hủy PO' }));
  await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  const [, init] = vi.mocked(fetch).mock.calls.find(([path]) => path === '/api/purchase-orders/10/cancel')!;
  expect(JSON.parse(String(init?.body)).reason).toBe('Không còn nhu cầu');
});

it('disables cancel and close while confirmed PO has receiving activity and open inbound', async () => {
  currentOrder = { ...currentOrder, status: 'CONFIRMED', hasAnyInbound: true, openInboundCount: 1 };
  window.history.replaceState(null, '', '/purchasing/purchase-orders/10'); render(<App />);
  await screen.findByRole('heading', { name: 'PO-TEST' });
  expect(screen.getByRole('button', { name: 'Hủy PO' }).hasAttribute('disabled')).toBe(true);
  expect(screen.getByRole('button', { name: 'Đóng PO' }).hasAttribute('disabled')).toBe(true);
});
