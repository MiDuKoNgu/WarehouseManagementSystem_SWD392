export function formatDate(value: string | null | undefined) {
  if (!value) return 'Chưa có';
  const date = new Date(value.length === 10 ? `${value}T00:00:00` : value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString('vi-VN');
}
export function DraftBadge({ status }: { status: string }) {
  const labels: Record<string, string> = { DRAFT: 'Bản nháp', CONFIRMED: 'Đã xác nhận', CANCELLED: 'Đã hủy', CLOSED: 'Đã đóng' };
  return <span className={`po-status po-status-${status.toLowerCase()}`}><span />{labels[status.toUpperCase()] || status}</span>;
}
export function PoError({ message, retry }: { message: string; retry?: () => void }) {
  return <div className="po-alert" role="alert"><p>{message}</p>{retry && <button type="button" className="utility-button" onClick={retry}>Thử lại</button>}</div>;
}
