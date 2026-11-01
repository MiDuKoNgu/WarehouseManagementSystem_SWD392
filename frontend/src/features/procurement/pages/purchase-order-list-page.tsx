import { Link } from 'react-router-dom';
import { useEffect, useMemo, useState } from 'react';
import { purchaseOrderApi } from '../api/purchase-order-api';
import { PurchasingLayout } from '../components/purchasing-layout';
import { DraftBadge, formatDate, PoError } from '../components/po-common';
import { usePoError } from '../hooks/use-po-error';
import type { PurchaseOrderSummary } from '../types/purchase-order';

export function PurchaseOrderListPage() {
  const [orders, setOrders] = useState<PurchaseOrderSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [attempt, setAttempt] = useState(0);
  const explain = usePoError();
  useEffect(() => {
    let cancelled = false; setLoading(true); setError('');
    purchaseOrderApi.list().then(data => { if (!cancelled) setOrders(data); })
      .catch(reason => { if (!cancelled) setError(explain(reason)); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [attempt, explain]);
  const filtered = useMemo(() => orders.filter(order => (!status || order.status.toUpperCase() === status) && `${order.poCode} ${order.supplierName} ${order.warehouseName}`.toLocaleLowerCase('vi').includes(search.trim().toLocaleLowerCase('vi'))), [orders, search, status]);
  return <PurchasingLayout>
    <div className="po-heading"><div><p className="workspace-eyebrow">MUA HÀNG</p><h1>Đơn mua hàng</h1><p>Từ kế hoạch mua đến hàng sẵn sàng nhận.</p></div><div className="po-actions"><Link className="utility-button" to="/purchasing/purchase-orders/import">↥ Import Excel</Link><Link className="po-primary-button" to="/purchasing/purchase-orders/new">+ Tạo đơn mua</Link></div></div>
    {error ? <PoError message={error} retry={() => setAttempt(value => value + 1)} /> : loading ? <div className="po-card po-loading" role="status">Đang tải đơn mua hàng…</div> : <>
      <div className="po-toolbar"><span>{orders.length} đơn mua hàng</span><label className="po-status-filter"><span className="sr-only">Lọc trạng thái PO</span><select value={status} onChange={event => setStatus(event.target.value)}><option value="">Tất cả trạng thái</option><option value="DRAFT">Bản nháp</option><option value="CONFIRMED">Đã xác nhận</option><option value="CANCELLED">Đã hủy</option><option value="CLOSED">Đã đóng</option></select></label><label className="po-search"><span className="sr-only">Tìm đơn mua hàng</span><input type="search" placeholder="Tìm mã PO, nhà cung cấp, kho…" value={search} onChange={event => setSearch(event.target.value)} /></label></div>
      {orders.length === 0 ? <section className="po-empty"><span className="po-empty-icon" aria-hidden="true">▤</span><h2>Đơn đầu tiên bắt đầu ở đây.</h2><p>Tạo bản nháp thủ công hoặc nhập từ file Excel. Đơn sẽ được lưu cùng nhật ký tạo mới.</p><Link className="po-primary-button" to="/purchasing/purchase-orders/new">Tạo đơn mua đầu tiên →</Link></section>
        : <div className="po-table-wrap po-card"><table className="po-table"><thead><tr><th>Mã đơn</th><th>Nhà cung cấp</th><th>Kho nhận</th><th>Ngày dự kiến</th><th className="po-number">Số lượng</th><th>Trạng thái</th></tr></thead><tbody>{filtered.map(order => <tr key={order.purchaseOrderId}><td><Link className="po-code-link" to={`/purchasing/purchase-orders/${order.purchaseOrderId}`}>{order.poCode}</Link><small>{formatDate(order.orderDate)}</small></td><td>{order.supplierName}</td><td>{order.warehouseName}</td><td>{formatDate(order.expectedArrivalDate)}</td><td className="po-number">{order.totalQuantity.toLocaleString('vi-VN')}<small>{order.lineCount} sản phẩm</small></td><td><DraftBadge status={order.status} /></td></tr>)}</tbody></table>{filtered.length === 0 && <p className="po-no-results">Không có đơn phù hợp với từ khóa.</p>}</div>}
    </>}
  </PurchasingLayout>;
}
