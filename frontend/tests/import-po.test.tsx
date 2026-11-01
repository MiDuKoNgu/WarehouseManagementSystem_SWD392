import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { App } from '../src/app/app';
import { purchaseOrderApi } from '../src/features/procurement/api/purchase-order-api';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

it('resolves supplier and warehouse names in the Excel preview and explains unresolved IDs', async () => {
  window.history.replaceState(null, '', '/purchasing/purchase-orders/import');
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ userId: 2, username: 'buyer', fullName: 'Buyer', role: 'PURCHASING_STAFF', warehouseId: null }), { status: 200, headers: { 'Content-Type': 'application/json' } })));
  vi.spyOn(purchaseOrderApi, 'options').mockResolvedValue({ suppliers: [{ supplierId: 7, supplierName: 'Nhà cung cấp Bắc' }], warehouses: [{ warehouseId: 12, warehouseName: 'Kho Thủ Đức', address: '' }], products: [] });
  vi.spyOn(purchaseOrderApi, 'preview').mockResolvedValue({ token: null, valid: false, errors: [], rows: [
    { rowNumber: 4, supplierId: 7, warehouseId: 12, productCode: 'TV-001', quantity: 5, errors: [] },
    { rowNumber: 5, supplierId: 99, warehouseId: 88, productCode: 'TV-002', quantity: 1, errors: ['Mã không hợp lệ'] },
    { rowNumber: 6, supplierId: null, warehouseId: null, productCode: 'TV-003', quantity: 1, errors: ['Thiếu thông tin'] },
  ] });
  render(<App />);
  const file = await screen.findByLabelText(/Chọn file Excel để kiểm tra/);
  fireEvent.change(file, { target: { files: [new File(['test'], 'purchase-order.xlsx')] } });
  fireEvent.submit(screen.getByRole('button', { name: 'Kiểm tra & xem trước →' }).closest('form')!);
  const table = await screen.findByRole('table');
  expect(within(table).getByRole('columnheader', { name: 'Nhà cung cấp / Kho nhận' })).toBeTruthy();
  const row = within(table).getByText('TV-001').closest('tr')!;
  expect(within(row).getByText('Nhà cung cấp Bắc')).toBeTruthy();
  expect(within(row).getByText('Kho nhận: Kho Thủ Đức')).toBeTruthy();
  expect(within(table).queryByText('7 / 12')).toBeNull();
  expect(within(table).getByText('Không tìm thấy nhà cung cấp (#99)')).toBeTruthy();
  expect(within(table).getByText('Kho nhận: Không tìm thấy kho (#88)')).toBeTruthy();
  expect(within(table).getByText('Chưa có nhà cung cấp')).toBeTruthy();
  expect(within(table).getByText('Kho nhận: Chưa có kho nhận')).toBeTruthy();
});
