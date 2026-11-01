import { useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../../app/providers/auth-provider';
import { BoxMark } from '../../../shared/components/ui/box-mark';

export function AdminLayout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function signOut() {
    setBusy(true);
    try { await logout(); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Chưa thể đăng xuất.'); }
    finally { setBusy(false); }
  }
  return <div className="workspace-shell audit-shell">
    <aside className="workspace-sidebar">
      <Link className="workspace-brand" to="/admin"><span><BoxMark /></span>EWMS</Link>
      <div className="workspace-switcher"><span className="workspace-avatar">{user?.fullName.slice(0, 1).toUpperCase()}</span><div><strong>Quản trị viên</strong><small>Không gian làm việc</small></div></div>
      <p className="sidebar-caption">ADMINISTRATION</p>
      <nav aria-label="Điều hướng quản trị"><Link className="sidebar-link" to="/admin"><span aria-hidden="true">⌂</span>Tổng quan</Link><Link className="sidebar-link active" to="/admin/audit-logs"><span aria-hidden="true">≡</span>Audit Logs</Link></nav>
      <div className="sidebar-bottom"><span className="account-name">{user?.fullName}<small>@{user?.username}</small></span><button className="utility-button" onClick={signOut} disabled={busy}>{busy ? 'Đang thoát…' : 'Đăng xuất'} ↗</button>{error && <p className="audit-error" role="alert">{error}</p>}</div>
    </aside>
    <div className="workspace-main"><header className="workspace-topbar"><span>EWMS <span className="breadcrumb-divider">/</span> Administration <span className="breadcrumb-divider">/</span> Audit Logs</span><span className="workspace-status"><span />Đã đăng nhập</span></header><main className="audit-document">{children}</main></div>
  </div>;
}
