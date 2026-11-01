import { Link } from 'react-router-dom';
import { useState, type ReactNode } from 'react';
import { useAuth } from '../../../app/providers/auth-provider';
import { BoxMark } from '../../../shared/components/ui/box-mark';

export function PurchasingLayout({ children, current = 'orders' }: { children: ReactNode; current?: 'orders' | 'import' | 'products' }) {
  const { user, logout } = useAuth();
  const [loggingOut, setLoggingOut] = useState(false);
  const [error, setError] = useState('');
  async function signOut() {
    setLoggingOut(true);
    try { await logout(); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Chưa thể đăng xuất.'); }
    finally { setLoggingOut(false); }
  }
  return <div className="workspace-shell procurement-shell">
    <aside className="workspace-sidebar">
      <Link className="workspace-brand" to="/purchasing"><span><BoxMark /></span>EWMS</Link>
      <div className="workspace-switcher"><span className="workspace-avatar">{user?.fullName.slice(0, 1).toUpperCase()}</span><div><strong>Nhân viên mua hàng</strong><small>Không gian làm việc</small></div></div>
      <p className="sidebar-caption">KHÔNG GIAN CỦA BẠN</p>
      <nav aria-label="Điều hướng mua hàng">
        <Link className="sidebar-link" to="/purchasing"><span aria-hidden="true">⌂</span>Tổng quan</Link>
        <Link className={`sidebar-link ${current === 'orders' ? 'active' : ''}`} to="/purchasing/purchase-orders"><span aria-hidden="true">▤</span>Đơn mua hàng</Link>
        <Link className={`sidebar-link ${current === 'import' ? 'active' : ''}`} to="/purchasing/purchase-orders/import"><span aria-hidden="true">↥</span>Import Excel</Link>
        <Link className={`sidebar-link ${current === 'products' ? 'active' : ''}`} to="/purchasing/products"><span aria-hidden="true">▦</span>Sản phẩm</Link>
      </nav>
      <div className="sidebar-bottom"><span className="account-name">{user?.fullName}<small>@{user?.username}</small></span><button className="utility-button" onClick={signOut} disabled={loggingOut}>{loggingOut ? 'Đang thoát…' : 'Đăng xuất'} ↗</button>{error && <p className="po-inline-error" role="alert">{error}</p>}</div>
    </aside>
    <div className="workspace-main"><header className="workspace-topbar"><span>EWMS <span className="breadcrumb-divider">/</span> Mua hàng <span className="breadcrumb-divider">/</span> {current === 'products' ? 'Sản phẩm' : 'Đơn mua hàng'}</span><span className="workspace-status"><span />Đã đăng nhập</span></header>
      <main className="po-document">{children}</main>
    </div>
  </div>;
}
