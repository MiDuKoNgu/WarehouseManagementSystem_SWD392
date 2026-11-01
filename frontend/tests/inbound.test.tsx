import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { App } from '../src/app/app';
import { inboundApi } from '../src/features/inbound/api';
import type { ReceiptDetail } from '../src/features/inbound/types';

const user = { userId: 3, username: 'staff', fullName: 'Nhân viên Kho', role: 'WAREHOUSE_STAFF', warehouseId: 1 };
const order = { purchaseOrderId: 10, poCode: 'PO-INBOUND-10', supplierId: 1, supplierName: 'Nhà cung cấp A', warehouseId: 1, warehouseName: 'Kho chính', status: 'CONFIRMED', lines: [{ poLineId: 11, productId: 1, productCode: 'TV-001', productName: 'Tivi A', orderedQuantity: 3, closedQuantity: 0 }, { poLineId: 12, productId: 2, productCode: 'TV-002', productName: 'Tivi B', orderedQuantity: 2, closedQuantity: 0 }] };
const original: ReceiptDetail = { receipt: { inboundId: 1, purchaseOrderId: 10, receiptCode: 'INB-001', status: 'DRAFT', receiptDate: '2026-11-01', createdAt: '2026-11-01T02:00:00Z', createdByUserId: 3, createdByName: 'Nhân viên Kho', completedAt: null, performedByUserId: null, performedByName: null, note: null, rowVersion: 'AAAAAAAAAAE=', warehouseId: 1, serialCount: 0 }, order, lines: order.lines.map(l => ({ ...l, receivedQuantity: 0, reservedQuantity: 0, currentQuantity: 0, remainingQuantity: l.orderedQuantity })), items: [], policy: { allowMultipleDrafts: false, acceptDamaged: true, allowCancellation: true } };
let detail: ReceiptDetail;
const response = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
beforeEach(() => {
  detail = structuredClone(original); sessionStorage.clear(); window.history.replaceState(null, '', '/warehouse/inbounds/1');
  vi.stubGlobal('fetch', vi.fn(async (path: string, init?: RequestInit) => {
    if (path === '/api/auth/me') return response(user);
    if (path === '/api/auth/csrf') return response({ token: 'inbound-csrf' });
    if (path === '/api/inbounds/orders') return response([order]);
    if (path === '/api/inbounds/orders/10') return response({ order, lines: detail.lines, policy: detail.policy });
    if (path === '/api/inbounds' && init?.method === 'POST') return response(detail, 201);
    if (path === '/api/inbounds') return response([{ receipt: detail.receipt, poCode: order.poCode, supplierName: order.supplierName, warehouseName: order.warehouseName, expectedQuantity: 5 }]);
    if (path === '/api/inbounds/1/items') {
      const body = JSON.parse(String(init?.body));
      detail = { ...detail, receipt: { ...detail.receipt, rowVersion: 'AAAAAAAAAAI=', serialCount: 1 }, items: [{ inboundItemId: 20, poLineId: body.poLineId, scannedSerial: body.serial, manufacturingMonth: body.manufacturingMonth, manufacturingYear: body.manufacturingYear, receivedCondition: body.receivedCondition, conditionNote: body.conditionNote, inputMethod: body.inputMethod, scannedByUserId: 3, scannedByName: 'Nhân viên Kho', scannedAt: '2026-11-01T03:00:00Z', serialItemId: null }] };
      return response(detail);
    }
    if (path === '/api/inbounds/1/complete') { detail = { ...detail, receipt: { ...detail.receipt, status: 'DONE', completedAt: '2026-11-01T04:00:00Z' } }; return response(detail); }
    if (path === '/api/inbounds/1/cancel') { detail = { ...detail, receipt: { ...detail.receipt, status: 'CANCELLED' }, items: [] }; return response(detail); }
    if (path === '/api/inbounds/1/items/20' && init?.method === 'DELETE') { detail = { ...detail, items: [], receipt: { ...detail.receipt, serialCount: 0 } }; return response(detail); }
    if (path === '/api/inbounds/1') return response(detail);
    throw new Error(`Unexpected ${init?.method || 'GET'} ${path}`);
  }));
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

async function enterSerial(value = 'SN-AbC-001') {
  await screen.findByRole('heading', { name: 'INB-001' });
  fireEvent.change(screen.getByLabelText('Tháng sản xuất'), { target: { value: '8' } });
  fireEvent.change(screen.getByLabelText('Năm sản xuất'), { target: { value: '2025' } });
  fireEvent.change(screen.getByLabelText('Serial'), { target: { value } });
}

it('saves manual serial with exact case, row version and CSRF; retains month/year and clears serial', async () => {
  render(<App />); await enterSerial(); fireEvent.submit(screen.getByRole('button', { name: 'Xác nhận thêm Serial' }).closest('form')!);
  await screen.findByText('SN-AbC-001');
  const [, init] = vi.mocked(fetch).mock.calls.find(([path]) => path === '/api/inbounds/1/items')!;
  expect(JSON.parse(String(init?.body))).toMatchObject({ serial: 'SN-AbC-001', manufacturingMonth: 8, manufacturingYear: 2025, rowVersion: 'AAAAAAAAAAE=', inputMethod: 'MANUAL', poLineId: 11 });
  expect((init?.headers as Record<string, string>)['X-CSRF-TOKEN']).toBe('inbound-csrf');
  expect((screen.getByLabelText('Serial') as HTMLInputElement).value).toBe('');
  expect((screen.getByLabelText('Tháng sản xuất') as HTMLSelectElement).value).toBe('8');
  expect((screen.getByLabelText('Năm sản xuất') as HTMLInputElement).value).toBe('2025');
});

it('clears production metadata when changing product and rejects future production', async () => {
  render(<App />); await enterSerial();
  fireEvent.change(screen.getByLabelText('Sản phẩm'), { target: { value: '12' } });
  expect((screen.getByLabelText('Tháng sản xuất') as HTMLSelectElement).value).toBe('');
  expect((screen.getByLabelText('Năm sản xuất') as HTMLInputElement).value).toBe('');
  fireEvent.change(screen.getByLabelText('Tháng sản xuất'), { target: { value: '12' } });
  fireEvent.change(screen.getByLabelText('Năm sản xuất'), { target: { value: '9999' } });
  fireEvent.change(screen.getByLabelText('Serial'), { target: { value: 'SN-FUTURE' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Xác nhận thêm Serial' }).closest('form')!);
  await screen.findByText('Kiểm tra Serial, sản phẩm và tháng/năm sản xuất; không được chọn thời gian tương lai.');
  expect(vi.mocked(fetch).mock.calls.some(([path]) => path === '/api/inbounds/1/items')).toBe(false);
});

it('requires a damage description and respects server policy hiding damaged acceptance', async () => {
  render(<App />); await enterSerial();
  fireEvent.change(screen.getByLabelText('Tình trạng'), { target: { value: 'DAMAGED' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Xác nhận thêm Serial' }).closest('form')!);
  await screen.findByText('Hàng hỏng cần mô tả tình trạng.');
  expect(vi.mocked(fetch).mock.calls.some(([path]) => path === '/api/inbounds/1/items')).toBe(false);
  cleanup(); detail.policy.acceptDamaged = false; render(<App />); await screen.findByRole('heading', { name: 'INB-001' });
  expect(screen.queryByRole('option', { name: /DAMAGED/ })).toBeNull();
});

it('requires explicit completion confirmation and locks the done receipt', async () => {
  render(<App />); await enterSerial(); fireEvent.submit(screen.getByRole('button', { name: 'Xác nhận thêm Serial' }).closest('form')!); await screen.findByText('SN-AbC-001');
  fireEvent.click(screen.getByRole('button', { name: 'Hoàn tất phiếu nhập' }));
  expect(vi.mocked(fetch).mock.calls.some(([path]) => path === '/api/inbounds/1/complete')).toBe(false);
  const dialog = screen.getByRole('dialog'); fireEvent.click(within(dialog).getByRole('button', { name: 'Xác nhận' }));
  await screen.findByText('Đã hoàn tất');
  expect(screen.queryByRole('button', { name: 'Xác nhận thêm Serial' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Sửa Serial' })).toBeNull();
  const [, init] = vi.mocked(fetch).mock.calls.find(([path]) => path === '/api/inbounds/1/complete')!;
  expect(JSON.parse(String(init?.body)).rowVersion).toBe('AAAAAAAAAAI=');
});

it('refuses severely damaged devices without saving a serial even if form submission is forced', async () => {
  render(<App />); await enterSerial('SEVERE-DAMAGE-001');
  fireEvent.change(screen.getByLabelText('Tình trạng'), { target: { value: 'REJECTED' } });
  expect(screen.getByText('Không tiếp nhận thiết bị này. Không thêm Serial vào phiếu; tách hàng hỏng nặng để xử lý với nhà cung cấp.')).toBeTruthy();
  const submit = screen.getByRole('button', { name: 'Xác nhận thêm Serial' }) as HTMLButtonElement;
  expect(submit.disabled).toBe(true); fireEvent.submit(submit.closest('form')!);
  await screen.findByText('Không tiếp nhận hàng hỏng nặng. Không được thêm Serial này vào phiếu.');
  expect(vi.mocked(fetch).mock.calls.some(([path]) => path === '/api/inbounds/1/items')).toBe(false);
});

it('accepts explicitly selected minor damage with a required condition description', async () => {
  render(<App />); await enterSerial('MINOR-DENT-001');
  expect(screen.getByRole('option', { name: 'DAMAGED — Hỏng nhẹ / móp méo — nhận cách ly' })).toBeTruthy();
  fireEvent.change(screen.getByLabelText('Tình trạng'), { target: { value: 'DAMAGED' } });
  fireEvent.change(screen.getByLabelText('Ghi chú tình trạng'), { target: { value: 'Móp nhẹ vỏ ngoài, đã kiểm tra và chấp nhận cách ly.' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Xác nhận thêm Serial' }).closest('form')!);
  await screen.findByText('MINOR-DENT-001');
  const [, init] = vi.mocked(fetch).mock.calls.find(([path]) => path === '/api/inbounds/1/items')!;
  expect(JSON.parse(String(init?.body))).toMatchObject({ receivedCondition: 'DAMAGED', conditionNote: 'Móp nhẹ vỏ ngoài, đã kiểm tra và chấp nhận cách ly.' });
});

it('requires a reason before canceling a draft receipt and records that reason in the request', async () => {
  render(<App />); await screen.findByRole('heading', { name: 'INB-001' });
  fireEvent.click(screen.getByRole('button', { name: 'Hủy phiếu' }));
  const dialog = screen.getByRole('dialog'); const form = within(dialog).getByRole('button', { name: 'Xác nhận' }).closest('form')!;
  fireEvent.submit(form); expect(within(dialog).getByText('Vui lòng nhập lý do hủy phiếu.')).toBeTruthy();
  expect(vi.mocked(fetch).mock.calls.some(([path]) => path === '/api/inbounds/1/cancel')).toBe(false);
  fireEvent.change(within(dialog).getByLabelText('Lý do hủy'), { target: { value: 'Nhà cung cấp chưa giao hàng.' } }); fireEvent.submit(form);
  await screen.findByText('Đã hủy phiếu nhập.');
  const [, init] = vi.mocked(fetch).mock.calls.find(([path]) => path === '/api/inbounds/1/cancel')!;
  expect(JSON.parse(String(init?.body))).toMatchObject({ reason: 'Nhà cung cấp chưa giao hàng.', rowVersion: 'AAAAAAAAAAE=' });
});

it('removes a draft scan only after confirming and keeps rowversion in DELETE payload', async () => {
  render(<App />); await enterSerial(); fireEvent.submit(screen.getByRole('button', { name: 'Xác nhận thêm Serial' }).closest('form')!); await screen.findByText('SN-AbC-001');
  fireEvent.click(screen.getByRole('button', { name: 'Hủy Serial' })); fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Xác nhận' }));
  await screen.findByText('Đã hủy hiệu lực Serial. Lịch sử tiếp nhận được giữ lại.');
  const [, init] = vi.mocked(fetch).mock.calls.find(([path]) => path === '/api/inbounds/1/items/20')!;
  expect(init?.method).toBe('DELETE'); expect(JSON.parse(String(init?.body)).rowVersion).toBe('AAAAAAAAAAI=');
});

it('creates a receipt from an eligible PO and retains the durable token across failed retries', async () => {
  window.history.replaceState(null, '', '/warehouse/inbounds/new'); render(<App />);
  await screen.findByRole('option', { name: /PO-INBOUND-10/ }); fireEvent.change(screen.getByLabelText('Purchase Order'), { target: { value: '10' } });
  await screen.findByText('Nhà cung cấp A → Kho chính'); fireEvent.submit(screen.getByRole('button', { name: 'Tạo phiếu và tiếp nhận →' }).closest('form')!);
  await screen.findByRole('heading', { name: 'INB-001' });
  const [, init] = vi.mocked(fetch).mock.calls.find(([path, init]) => path === '/api/inbounds' && init?.method === 'POST')!;
  expect(JSON.parse(String(init?.body)).token).toMatch(/^[0-9a-f-]{36}$/i);
  expect(sessionStorage.getItem('ewms-inbound-create-10')).toBeNull();
  let fail = true; const tokens: string[] = [];
  vi.stubGlobal('fetch', vi.fn(async (path: string, init?: RequestInit) => { if (path === '/api/auth/csrf') return response({ token: 'csrf' }); tokens.push(JSON.parse(String(init?.body)).token); if (fail) return response({ message: 'timeout' }, 503); return response(detail); }));
  await expect(inboundApi.create(10, '')).rejects.toThrow('timeout'); fail = false; await inboundApi.create(10, ''); expect(tokens[0]).toBe(tokens[1]);
});

it('filters receipt list by code, state and receipt date', async () => {
  window.history.replaceState(null, '', '/warehouse/inbounds'); render(<App />); await screen.findByText('INB-001');
  fireEvent.change(screen.getByLabelText('Tìm mã phiếu / PO'), { target: { value: 'PO-INBOUND-10' } }); expect(screen.getByText('INB-001')).toBeTruthy();
  fireEvent.change(screen.getByLabelText('Trạng thái'), { target: { value: 'DONE' } }); expect(screen.queryByText('INB-001')).toBeNull();
  fireEvent.change(screen.getByLabelText('Trạng thái'), { target: { value: '' } });
  fireEvent.change(screen.getByLabelText('Từ ngày'), { target: { value: '2026-11-02' } }); expect(screen.queryByText('INB-001')).toBeNull();
});

it('denies purchasing access before any inbound request', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => response({ ...user, role: 'PURCHASING_STAFF', warehouseId: null })));
  render(<App />); await screen.findByRole('heading', { name: 'Bạn đang ở nhầm không gian.' });
  expect(vi.mocked(fetch).mock.calls.every(([path]) => path === '/api/auth/me')).toBe(true);
});

it('clears canceled/conflicted create token after a definitive 409', async () => {
  vi.stubGlobal('fetch', vi.fn(async (path: string) => path === '/api/auth/csrf' ? response({ token: 'csrf' }) : response({ message: 'Phiếu đã hủy' }, 409)));
  await expect(inboundApi.create(10, '')).rejects.toThrow('Phiếu đã hủy');
  expect(sessionStorage.getItem('ewms-inbound-create-10')).toBeNull();
});

it('labels legacy scan sources honestly and requires MANUAL source when editing unknown legacy metadata', async () => {
  render(<App />); await enterSerial(); fireEvent.submit(screen.getByRole('button', { name: 'Xác nhận thêm Serial' }).closest('form')!); await screen.findByText('SN-AbC-001'); cleanup();
  detail.items[0].inputMethod = 'UNKNOWN'; render(<App />); await screen.findByText('Chưa ghi nhận (dữ liệu cũ)');
  fireEvent.click(screen.getByRole('button', { name: 'Sửa Serial' }));
  await screen.findByRole('button', { name: 'Lưu thay đổi Serial' });
  expect(screen.getByText('Nguồn: Nhập tay · Enter để thêm nhanh.')).toBeTruthy();
});

it('retries terminal receipt synchronization without showing serial editing actions', async () => {
  detail.receipt.status = 'DONE'; detail.receipt.completedAt = '2026-11-01T04:00:00Z';
  render(<App />); await screen.findByRole('heading', { name: 'INB-001' });
  fireEvent.click(screen.getByRole('button', { name: 'Đồng bộ trạng thái PO' }));
  await screen.findByText('Đã đồng bộ trạng thái nhận hàng với PO.');
  expect(screen.queryByRole('button', { name: 'Xác nhận thêm Serial' })).toBeNull();
  const [, init] = vi.mocked(fetch).mock.calls.find(([path]) => path === '/api/inbounds/1/complete')!;
  expect(JSON.parse(String(init?.body)).rowVersion).toBe('AAAAAAAAAAE=');
});

it('shows accepted serial metadata and keeps browsing Putaway read-only until confirmation', async () => {
  const previous = vi.mocked(fetch).getMockImplementation()!;
  vi.stubGlobal('fetch', vi.fn(async (path: string, init?: RequestInit) => path.startsWith('/api/putaway/pending?') ? response({ items: [{ serialItemId: 99, serialNumber: 'PENDING-SERIAL-99', productId: 1, originalInboundId: 1, receiptCode: 'INB-001', itemCondition: 'GOOD', isQuarantined: false, receivedAt: '2026-11-01T04:00:00Z', warehouseName: 'Kho A', hasActiveClaim: false, product: { productCode: 'TV-001', productName: 'Ti vi Samsung 55 inch', categoryName: 'Ti vi', brandName: 'Samsung' } }], totalCount: 1, page: 1, pageSize: 20 }) : previous(path, init)));
  window.history.replaceState(null, '', '/warehouse/pending-putaway'); render(<App />);
  await screen.findByRole('heading', { name: 'PENDING-SERIAL-99' });
  expect(screen.getByRole('link', { name: 'INB-001' }).getAttribute('href')).toBe('/warehouse/inbounds/1');
  expect(screen.getByText('Ti vi Samsung 55 inch')).toBeTruthy();
  expect(vi.mocked(fetch).mock.calls.every(([, init]) => !init?.method || init.method === 'GET')).toBe(true);
});
