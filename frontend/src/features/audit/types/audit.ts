export interface AuditItem {
  source: 'procurement' | 'inventory';
  auditLogId: number;
  userId: number;
  actorName: string | null;
  businessGroup: string;
  action: string;
  entityName: string;
  entityId: string;
  documentCode: string | null;
  status: string;
  createdAt: string;
}
export interface AuditDetail { item: AuditItem; oldValues: string | null; newValues: string | null }
export interface AuditPage { items: AuditItem[]; totalCount: number; hasMore: boolean; nextCursor: string | null; pageSize: number }
export interface AuditOptions { employees: { userId: number; username: string; fullName: string }[]; actions: string[] }
export interface AuditFilters { userId: string; businessGroup: string; action: string; documentCode: string; from: string; to: string }
export const emptyFilters: AuditFilters = { userId: '', businessGroup: '', action: '', documentCode: '', from: '', to: '' };
export const groupLabels: Record<string, string> = { PO: 'Đơn mua hàng', INBOUND: 'Nhập hàng', PUTAWAY: 'Xếp hàng vào kho' };
export const actionLabels: Record<string, string> = {
  CREATE_PO: 'Tạo PO', UPDATE_PO: 'Chỉnh sửa PO', EDIT_PO: 'Chỉnh sửa PO', CONFIRM_PO: 'Xác nhận PO',
  CANCEL_PO: 'Hủy PO', CLOSE_PO: 'Đóng PO', IMPORT_PO_EXCEL: 'Import PO từ Excel',
  CREATE_INBOUND: 'Tạo phiếu nhập', UPDATE_INBOUND: 'Cập nhật phiếu nhập', COMPLETE_INBOUND: 'Hoàn tất nhập hàng',
  CONFIRM_PUTAWAY: 'Xác nhận xếp hàng vào kho',
};
export const formatAuditTime = (value: string) => new Intl.DateTimeFormat('vi-VN', {
  dateStyle: 'short', timeStyle: 'medium', timeZone: 'Asia/Ho_Chi_Minh',
}).format(new Date(value));

export function auditQuery(filters: AuditFilters, cursor = '', pageSize = 20): URLSearchParams {
  const query = new URLSearchParams({ pageSize: String(pageSize) });
  for (const name of ['userId', 'businessGroup', 'action', 'documentCode'] as const)
    if (filters[name].trim()) query.set(name, filters[name].trim());
  if (filters.from) query.set('from', `${filters.from}T00:00:00+07:00`);
  if (filters.to) {
    const next = new Date(`${filters.to}T00:00:00+07:00`);
    next.setUTCDate(next.getUTCDate() + 1);
    query.set('to', next.toISOString());
  }
  if (cursor) query.set('cursor', cursor);
  return query;
}
