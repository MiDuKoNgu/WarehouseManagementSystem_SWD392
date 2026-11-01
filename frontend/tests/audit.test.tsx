import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { App } from '../src/app/app';
import { auditQuery, emptyFilters, type AuditItem } from '../src/features/audit/types/audit';
import { AuditValues } from '../src/features/audit/components/audit-values';

const admin = { userId: 1, username: 'admin', fullName: 'Admin Test', role: 'ADMIN', warehouseId: null };
const item: AuditItem = { source: 'procurement', auditLogId: 1, userId: 2, actorName: 'Buyer Test', businessGroup: 'PO', action: 'CREATE_PO', entityName: 'PurchaseOrder', entityId: '10', documentCode: 'PO-TEST-10', status: 'SUCCESS', createdAt: '2026-11-01T01:30:00Z' };
const reply = (data: unknown) => new Response(JSON.stringify(data), { status: 200, headers: { 'Content-Type': 'application/json' } });
const options = { employees: [{ userId: 2, username: 'buyer', fullName: 'Buyer Test' }], actions: ['CREATE_PO', 'UPDATE_PO'] };

beforeEach(() => {
  window.history.replaceState(null, '', '/admin/audit-logs');
  vi.stubGlobal('fetch', vi.fn(async (path: string) => {
    if (path === '/api/auth/me') return reply(admin);
    if (path === '/api/admin/audit-logs/options') return reply(options);
    if (path === '/api/admin/audit-logs/procurement/1') return reply({ item, oldValues: '{"status":"Draft","note":"<script>alert(1)</script>"}', newValues: '{"status":"Confirmed"}' });
    if (path.startsWith('/api/admin/audit-logs?')) {
      const cursor = new URL(path, 'http://localhost').searchParams.get('cursor');
      return reply({ items: [cursor ? { ...item, auditLogId: 2, documentCode: 'PO-OLDER' } : item], totalCount: 2, hasMore: !cursor, nextCursor: cursor ? null : 'page-2', pageSize: 20 });
    }
    throw new Error(`Unexpected API ${path}`);
  }));
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('converts a Vietnam date range to inclusive start and exclusive next-day end', () => {
  const query = auditQuery({ ...emptyFilters, from: '2026-11-01', to: '2026-11-01' });
  expect(query.get('from')).toBe('2026-11-01T00:00:00+07:00');
  expect(query.get('to')).toBe('2026-11-01T17:00:00.000Z');
});

it('paginates by server cursor and resets pagination when applying employee/document/date filters', async () => {
  render(<App />);
  await screen.findByText('PO-TEST-10');
  fireEvent.click(screen.getByRole('button', { name: 'Sau →' }));
  await screen.findByText('PO-OLDER');
  expect(new URLSearchParams(window.location.search).get('cursor')).toBe('page-2');
  fireEvent.click(screen.getByRole('button', { name: '← Trước' }));
  await screen.findByText('PO-TEST-10');
  expect(new URLSearchParams(window.location.search).get('cursor')).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Sau →' }));
  await screen.findByText('PO-OLDER');
  fireEvent.change(screen.getByLabelText('Nhân viên'), { target: { value: '2' } });
  fireEvent.change(screen.getByLabelText('Mã chứng từ'), { target: { value: 'PO-TEST' } });
  fireEvent.change(screen.getByLabelText('Từ ngày'), { target: { value: '2026-11-01' } });
  fireEvent.change(screen.getByLabelText('Đến ngày'), { target: { value: '2026-11-01' } });
  fireEvent.click(screen.getByRole('button', { name: 'Áp dụng bộ lọc →' }));
  await screen.findByText('PO-TEST-10');
  const listCalls = vi.mocked(fetch).mock.calls.filter(([path]) => String(path).startsWith('/api/admin/audit-logs?'));
  const query = new URL(String(listCalls.at(-1)![0]), 'http://localhost').searchParams;
  expect(query.get('cursor')).toBeNull();
  expect(query.get('userId')).toBe('2');
  expect(query.get('documentCode')).toBe('PO-TEST');
  expect(query.get('to')).toBe('2026-11-01T17:00:00.000Z');
  expect(screen.getByRole('button', { name: '← Trước' }).hasAttribute('disabled')).toBe(true);
  expect(screen.queryByRole('button', { name: /xóa|chỉnh sửa/i })).toBeNull();
});

it('shows before/after changes as safe text and restores list filters when returning', async () => {
  window.history.replaceState(null, '', '/admin/audit-logs?documentCode=PO-TEST');
  render(<App />);
  await screen.findByText('PO-TEST-10');
  fireEvent.click(screen.getByRole('link', { name: 'Xem chi tiết →' }));
  await screen.findByRole('heading', { name: 'Nội dung thay đổi' });
  expect(screen.getByText('"Draft"')).toBeTruthy();
  expect(screen.getByText('"Confirmed"')).toBeTruthy();
  expect(document.querySelector('script')).toBeNull();
  expect(screen.queryByRole('button', { name: /xóa|chỉnh sửa/i })).toBeNull();
  fireEvent.click(screen.getByRole('link', { name: '← Lịch sử thao tác' }));
  await screen.findByText('PO-TEST-10');
  expect(new URLSearchParams(window.location.search).get('documentCode')).toBe('PO-TEST');
});

it('denies non-admin users before calling any audit endpoint', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => reply({ ...admin, userId: 2, role: 'PURCHASING_STAFF' })));
  render(<App />);
  await screen.findByRole('heading', { name: 'Bạn đang ở nhầm không gian.' });
  await waitFor(() => expect(vi.mocked(fetch).mock.calls.every(([path]) => path === '/api/auth/me')).toBe(true));
});

it('renders missing and malformed legacy audit snapshots without crashing or interpreting HTML', () => {
  render(<AuditValues oldValues={null} newValues={'<img src=x onerror=alert(1)>'} />);
  expect(screen.getByText('Không có dữ liệu.')).toBeTruthy();
  expect(screen.getByText('<img src=x onerror=alert(1)>')).toBeTruthy();
  expect(document.querySelector('img')).toBeNull();
});
