import { Link } from 'react-router-dom';
import { useEffect, useState } from 'react';
import { useAuth } from '../../../app/providers/auth-provider';
import { BoxMark } from '../../../shared/components/ui/box-mark';
import { ApiError } from '../../../shared/lib/http-client';
import { authApi } from '../../auth/api/auth-api';
import { roleLabels, type UserProfile, type UserRole } from '../../auth/types/auth';

const content: Record<UserRole, {
  endpoint: string; title: string; description: string; icon: string;
  areas: { title: string; description: string; icon: string }[];
}> = {
  ADMIN: {
    endpoint: 'admin', title: 'Một hệ thống. Cùng vận hành.', icon: '✳',
    description: 'Không gian quản trị để giữ con người, quyền truy cập và hoạt động kho đúng chỗ.',
    areas: [
      { title: 'Tài khoản & phân quyền', description: 'Cấp quyền phù hợp cho từng thành viên trong hệ thống.', icon: '♙' },
      { title: 'Kho & phân công', description: 'Gán nhân viên vào kho trước khi bắt đầu vận hành.', icon: '⌂' },
      { title: 'Nhật ký hoạt động', description: 'Theo dõi thay đổi trong quy trình mua hàng và nhập kho.', icon: '≡' },
    ],
  },
  PURCHASING_STAFF: {
    endpoint: 'purchasing', title: 'Mua đúng hàng. Sẵn sàng nhận.', icon: '↗',
    description: 'Không gian mua hàng kết nối nhà cung cấp, sản phẩm và kế hoạch nhập kho.',
    areas: [
      { title: 'Đơn mua hàng', description: 'Chuẩn bị, xác nhận và theo dõi đơn mua hàng.', icon: '▤' },
      { title: 'Nhà cung cấp', description: 'Thông tin đối tác và những sản phẩm được cung ứng.', icon: '♧' },
      { title: 'Danh mục sản phẩm', description: 'Tra cứu hàng hóa trước khi lập kế hoạch mua hàng.', icon: '▦' },
    ],
  },
  WAREHOUSE_STAFF: {
    endpoint: 'warehouse', title: 'Đúng hàng. Đúng vị trí.', icon: '⌂',
    description: 'Không gian vận hành dành cho kho được phân công. Mỗi món hàng đều có một chỗ.',
    areas: [
      { title: 'Nhận hàng', description: 'Đối chiếu đơn mua hàng và kiểm tra serial khi nhận.', icon: '↓' },
      { title: 'Xếp hàng vào kho', description: 'Đưa hàng đã nhận đến vị trí lưu trữ phù hợp.', icon: '▦' },
      { title: 'Tra cứu tồn kho', description: 'Theo dõi serial và vị trí hàng trong kho được gán.', icon: '⌕' },
    ],
  },
};

export function WorkspacePage({ user }: { user: UserProfile }) {
  const page = content[user.role];
  const { logout, refresh } = useAuth();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [loggingOut, setLoggingOut] = useState(false);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError('');
    authApi.workspace(page.endpoint).catch((reason: unknown) => {
      if (cancelled) return;
      setError(reason instanceof Error ? reason.message : 'Không thể tải không gian làm việc.');
      if (reason instanceof ApiError && reason.status === 401) void refresh();
    }).finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [page.endpoint, attempt, refresh]);

  async function signOut() {
    setLoggingOut(true);
    try { await logout(); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Chưa thể đăng xuất. Vui lòng thử lại.'); }
    finally { setLoggingOut(false); }
  }

  return (
    <div className="workspace-shell">
      <aside className="workspace-sidebar">
        <a className="workspace-brand" href="#overview"><span><BoxMark /></span>EWMS</a>
        <div className="workspace-switcher"><span className="workspace-avatar">{user.fullName.slice(0, 1).toUpperCase()}</span><div><strong>{roleLabels[user.role]}</strong><small>Không gian làm việc</small></div></div>
        <p className="sidebar-caption">KHÔNG GIAN CỦA BẠN</p>
        <nav aria-label="Điều hướng không gian làm việc">
          <a className="sidebar-link active" href="#overview"><span aria-hidden="true">⌂</span>Tổng quan</a>
          {user.role === 'PURCHASING_STAFF' && <Link className="sidebar-link" to="/purchasing/purchase-orders"><span aria-hidden="true">▤</span>Đơn mua hàng</Link>}
          {user.role === 'PURCHASING_STAFF' && <Link className="sidebar-link" to="/purchasing/purchase-orders/import"><span aria-hidden="true">↥</span>Import Excel</Link>}
          {user.role === 'PURCHASING_STAFF' && <Link className="sidebar-link" to="/purchasing/products"><span aria-hidden="true">▦</span>Sản phẩm</Link>}
          {user.role === 'WAREHOUSE_STAFF' && <Link className="sidebar-link" to="/warehouse/inbounds"><span aria-hidden="true">↓</span>Phiếu nhập kho</Link>}
          {user.role === 'WAREHOUSE_STAFF' && <Link className="sidebar-link" to="/warehouse/pending-putaway"><span aria-hidden="true">▦</span>Chờ Putaway</Link>}
          {user.role === 'ADMIN' && <><p className="sidebar-caption">ADMINISTRATION</p><Link className="sidebar-link" to="/admin/audit-logs"><span aria-hidden="true">≡</span>Audit Logs</Link></>}
          <a className="sidebar-link" href="#account"><span aria-hidden="true">♙</span>Tài khoản của tôi</a>
        </nav>
        {user.role === 'WAREHOUSE_STAFF' && <div className="warehouse-assignment"><span aria-hidden="true">▦</span><div>Kho được phân công<strong>Kho #{user.warehouseId}</strong></div></div>}
        <div className="sidebar-bottom"><span className="account-name">{user.fullName}<small>@{user.username}</small></span><button className="utility-button" onClick={signOut} disabled={loggingOut}>{loggingOut ? 'Đang thoát…' : 'Đăng xuất'} <span aria-hidden="true">↗</span></button></div>
      </aside>
      <div className="workspace-main">
        <header className="workspace-topbar"><span>EWMS <span className="breadcrumb-divider">/</span> {roleLabels[user.role]}</span><span className="workspace-status"><span />Đã đăng nhập</span></header>
        <main className="workspace-document" id="overview">
          {loading ? <div className="workspace-notice" role="status">Đang mở không gian làm việc…</div>
            : error ? <div className="workspace-notice" role="alert"><p>{error}</p><button className="utility-button" onClick={() => setAttempt(value => value + 1)}>Thử lại</button></div>
            : <>
              <div className="workspace-sticker" aria-hidden="true">{page.icon}</div>
              <p className="workspace-eyebrow">{roleLabels[user.role]}</p>
              <h1>{page.title}</h1>
              <p className="workspace-description">{page.description}</p>
              <div className="welcome-note"><span aria-hidden="true">☀</span><p>Chào <strong>{user.fullName}</strong>,<br /><span>bạn đã vào đúng không gian làm việc của mình.</span></p></div>
              <section className="workspace-section" aria-labelledby="area-title"><div className="section-heading"><h2 id="area-title">Phạm vi công việc</h2><span className="quiet-badge">{roleLabels[user.role]}</span></div>
                <div className="work-area-list">{page.areas.map((area, index) => <article className="work-area" key={area.title}><span className={`area-icon area-icon-${index}`} aria-hidden="true">{area.icon}</span><div><h3>{area.title}</h3><p>{area.description}</p></div>{user.role === 'PURCHASING_STAFF' && index === 0 ? <Link className="po-code-link po-area-link" to="/purchasing/purchase-orders">Mở đơn mua →</Link> : user.role === 'PURCHASING_STAFF' && index === 2 ? <Link className="po-code-link po-area-link" to="/purchasing/products">Mở sản phẩm →</Link> : user.role === 'ADMIN' && index === 2 ? <Link className="po-code-link po-area-link" to="/admin/audit-logs">Xem lịch sử →</Link> : user.role === 'WAREHOUSE_STAFF' && index === 1 ? <Link className="po-code-link po-area-link" to="/warehouse/pending-putaway">Xem chờ xếp →</Link> : user.role === 'WAREHOUSE_STAFF' && index === 0 ? <Link className="po-code-link po-area-link" to="/warehouse/inbounds">Mở phiếu nhập →</Link> : <span className="coming-label">Sắp triển khai</span>}</article>)}</div>
              </section>
              <section className="workspace-section account-section" id="account" aria-labelledby="account-title"><h2 id="account-title">Tài khoản của bạn</h2><dl className="account-details"><div><dt>Họ và tên</dt><dd>{user.fullName}</dd></div><div><dt>Tên đăng nhập</dt><dd>{user.username}</dd></div><div><dt>Vai trò</dt><dd>{roleLabels[user.role]}</dd></div><div><dt>Phạm vi kho</dt><dd>{user.role === 'WAREHOUSE_STAFF' ? `Kho #${user.warehouseId}` : 'Không giới hạn theo kho được gán'}</dd></div></dl></section>
              <footer className="workspace-footer">EWMS · Electronics Warehouse Management</footer>
            </>}
        </main>
      </div>
    </div>
  );
}
