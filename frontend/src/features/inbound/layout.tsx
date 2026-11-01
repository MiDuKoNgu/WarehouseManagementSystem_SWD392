import { useState, type ReactNode } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { useAuth } from '../../app/providers/auth-provider';
import { BoxMark } from '../../shared/components/ui/box-mark';
export function InboundLayout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  const path = useLocation().pathname;
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  async function signOut() { setBusy(true); try { await logout(); } catch (e) { setError(message(e)); } finally { setBusy(false); } }
  return <div className="workspace-shell procurement-shell inbound-shell"><aside className="workspace-sidebar">
    <Link className="workspace-brand" to="/warehouse"><span><BoxMark /></span>EWMS</Link>
    <div className="workspace-switcher"><span className="workspace-avatar">{user?.fullName.slice(0, 1)}</span><div><strong>Nhân viên kho</strong><small>Kho #{user?.warehouseId}</small></div></div>
    <p className="sidebar-caption">KHÔNG GIAN CỦA BẠN</p><nav aria-label="Điều hướng kho"><Link className="sidebar-link" to="/warehouse">⌂ Tổng quan</Link><Link className={`sidebar-link ${path.startsWith('/warehouse/inbounds') ? 'active' : ''}`} to="/warehouse/inbounds">↓ Phiếu nhập kho</Link><Link className={`sidebar-link ${path === '/warehouse/pending-putaway' ? 'active' : ''}`} to="/warehouse/pending-putaway">▦ Chờ Putaway</Link></nav>
    <div className="sidebar-bottom"><span className="account-name">{user?.fullName}<small>@{user?.username}</small></span><button className="utility-button" disabled={busy} onClick={() => void signOut()}>{busy ? 'Đang thoát…' : 'Đăng xuất'} ↗</button>{error && <p role="alert">{error}</p>}</div>
  </aside><div className="workspace-main"><header className="workspace-topbar"><span>EWMS / Kho / {path === '/warehouse/pending-putaway' ? 'Putaway' : 'Phiếu nhập kho'}</span><span className="workspace-status"><span />Đã đăng nhập</span></header><main className="po-document inbound-document">{children}</main></div></div>;
}
export function message(error: unknown) { return error instanceof Error ? error.message : 'Không thể thực hiện. Vui lòng thử lại.'; }
export function Status({ value }: { value: string }) { return <span className={`po-status inbound-status-${value.toLowerCase()}`}>{value === 'DRAFT' ? 'Đang tiếp nhận' : value === 'DONE' || value === 'COMPLETED' ? 'Đã hoàn tất' : value === 'CANCELLED' ? 'Đã hủy' : value}</span>; }
export function date(value: string | null) { return value ? new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : '—'; }
export function ErrorNotice({ error, retry }: { error: string; retry?: () => void }) { return error ? <div className="po-alert" role="alert"><p>{error}</p>{retry && <button className="utility-button" onClick={retry}>Tải lại</button>}</div> : null; }
