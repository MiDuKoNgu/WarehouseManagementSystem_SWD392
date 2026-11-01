import { LoginPage } from '../features/auth/pages/login-page';
import { BrowserRouter, Link, Navigate, useLocation } from 'react-router-dom';
import { AuthProvider, useAuth } from './providers/auth-provider';
import { rolePaths } from '../features/auth/types/auth';
import { WorkspacePage } from '../features/dashboard/pages/workspace-page';
import { PurchaseOrderListPage } from '../features/procurement/pages/purchase-order-list-page';
import { CreatePurchaseOrderPage } from '../features/procurement/pages/create-purchase-order-page';
import { ImportPurchaseOrderPage } from '../features/procurement/pages/import-purchase-order-page';
import { PurchaseOrderDetailPage } from '../features/procurement/pages/purchase-order-detail-page';
import '../features/procurement/styles/purchase-orders.css';
import { AuditListPage } from '../features/audit/pages/audit-list-page';
import { AuditDetailPage } from '../features/audit/pages/audit-detail-page';
import '../features/audit/styles/audit.css';
import { ProductListPage } from '../features/products/pages/product-list-page';
import { ProductDetailPage } from '../features/products/pages/product-detail-page';
import { ProductFormPage } from '../features/products/pages/product-form-page';
import '../features/products/styles/products.css';
import { InboundListPage } from '../features/inbound/list-page';
import { CreateInboundPage } from '../features/inbound/create-page';
import { InboundDetailPage } from '../features/inbound/detail-page';
import '../features/inbound/inbound.css';
import { PendingPutawayPage } from '../features/inbound/pending-putaway-page';

export function App() {
  return <BrowserRouter><AuthProvider><AppRoutes /></AuthProvider></BrowserRouter>;
}

function AppRoutes() {
  const { user, loading, error, refresh } = useAuth();
  const path = useLocation().pathname.replace(/\/$/, '') || '/';
  const home = user ? rolePaths[user.role] : '/login';
  if (path === '/') return <Navigate to="/login" replace />;
  if (path === '/login') return <>{error && <div className="connection-banner" role="alert">{error} <button onClick={() => void refresh()}>Thử lại</button></div>}<LoginPage /></>;
  if (loading) return <main className="session-loading" role="status">Đang kiểm tra phiên đăng nhập…</main>;
  if (!user) return <>{path !== '/login' && <Navigate to="/login" replace />}{error && <div className="connection-banner" role="alert">{error} <button onClick={() => void refresh()}>Thử lại</button></div>}<LoginPage /></>;
  if (user.role === 'ADMIN') {
    if (path === '/admin/audit-logs') return <AuditListPage />;
    const auditMatch = path.match(/^\/admin\/audit-logs\/(procurement|inventory)\/(\d+)$/);
    if (auditMatch && Number.isSafeInteger(Number(auditMatch[2])) && Number(auditMatch[2]) > 0)
      return <AuditDetailPage key={`${auditMatch[1]}-${auditMatch[2]}`} source={auditMatch[1]} id={Number(auditMatch[2])} />;
  }
  if (user.role === 'PURCHASING_STAFF' && path.startsWith('/purchasing/purchase-orders')) {
    if (path === '/purchasing/purchase-orders') return <PurchaseOrderListPage />;
    if (path === '/purchasing/purchase-orders/new') return <CreatePurchaseOrderPage />;
    if (path === '/purchasing/purchase-orders/import') return <ImportPurchaseOrderPage />;
    const edit = path.match(/^\/purchasing\/purchase-orders\/(\d+)\/edit$/);
    if (edit && Number.isSafeInteger(Number(edit[1])) && Number(edit[1]) > 0) return <CreatePurchaseOrderPage key={`edit-${edit[1]}`} id={Number(edit[1])} />;
    const match = path.match(/^\/purchasing\/purchase-orders\/(\d+)$/);
    if (match && Number.isSafeInteger(Number(match[1])) && Number(match[1]) > 0) return <PurchaseOrderDetailPage key={match[1]} id={Number(match[1])} />;
  }
  if (user.role === 'PURCHASING_STAFF' && path.startsWith('/purchasing/products')) {
    if (path === '/purchasing/products') return <ProductListPage />;
    if (path === '/purchasing/products/new') return <ProductFormPage key="new-product" />;
    const product = path.match(/^\/purchasing\/products\/(\d+)(\/edit)?$/);
    if (product && Number.isSafeInteger(Number(product[1])) && Number(product[1]) > 0)
      return product[2] ? <ProductFormPage key={`product-edit-${product[1]}`} id={Number(product[1])} /> : <ProductDetailPage key={`product-${product[1]}`} id={Number(product[1])} />;
  }
  if (user.role === 'WAREHOUSE_STAFF') {
    if (path === '/warehouse/pending-putaway') return <PendingPutawayPage />;
    if (path === '/warehouse/inbounds') return <InboundListPage />;
    if (path === '/warehouse/inbounds/new') return <CreateInboundPage />;
    const receipt = path.match(/^\/warehouse\/inbounds\/(\d+)$/);
    if (receipt && Number.isSafeInteger(Number(receipt[1])) && Number(receipt[1]) > 0) return <InboundDetailPage key={receipt[1]} id={Number(receipt[1])} />;
  }
  if (path !== home)
    return <main className="access-denied"><span aria-hidden="true">↗</span><h1>Bạn đang ở nhầm không gian.</h1><p>Trang này không thuộc quyền truy cập của tài khoản hiện tại.</p><Link to={home}>Về trang của tôi →</Link></main>;
  return <WorkspacePage key={user.userId} user={user} />;
}
