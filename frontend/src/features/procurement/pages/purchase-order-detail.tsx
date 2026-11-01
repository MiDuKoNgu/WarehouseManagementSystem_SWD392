import { Link, useLocation } from 'react-router-dom';
import { useEffect, useRef, useState, type FormEvent } from 'react';
import { purchaseOrderApi } from '../api/purchase-order-api';
import { PurchasingLayout } from '../components/purchasing-layout';
import { DraftBadge, formatDate, PoError } from '../components/po-common';
import { PoTransitionDialog, transitionLabels, type PoTransition } from '../components/po-transition-dialog';
import { usePoError } from '../hooks/use-po-error';
import type { PurchaseOrderDetail } from '../types/purchase-order';
import { ApiError } from '../../../shared/lib/http-client';

export function PurchaseOrderDetailPage({ id }: { id: number }) {
  const [order, setOrder] = useState<PurchaseOrderDetail | null>(null);
  const [error, setError] = useState(''); const [attempt, setAttempt] = useState(0);
  const [action, setAction] = useState<PoTransition | null>(null);
  const [reason, setReason] = useState(''); const [actionError, setActionError] = useState('');
  const [busy, setBusy] = useState(false); const inFlight = useRef(false);
  const [notice, setNotice] = useState('');
  const explain = usePoError();
  const query = new URLSearchParams(useLocation().search);
  useEffect(() => {
    let cancelled = false; setError(''); setOrder(null);
    purchaseOrderApi.detail(id).then(data => { if (!cancelled) setOrder(data); })
      .catch(problem => { if (!cancelled) setError(explain(problem)); });
    return () => { cancelled = true; };
  }, [id, attempt, explain]);
  function start(next: PoTransition) { setReason(''); setActionError(''); setAction(next); }
  async function submit(event: FormEvent) {
    event.preventDefault(); if (!order || !action || inFlight.current) return;
    if (action !== 'confirm' && !reason.trim()) { setActionError('Vui lòng nhập lý do.'); return; }
    inFlight.current = true; setBusy(true); setActionError('');
    try {
      const updated = await purchaseOrderApi.transition(id, action, order.rowVersion, action === 'confirm' ? undefined : reason.trim());
      setOrder(updated); setNotice(`${transitionLabels[action]} thành công. Lịch sử thao tác đã được lưu.`); setAction(null);
    } catch (problem) {
      if (problem instanceof ApiError && problem.status === 409) {
        setAction(null); setNotice(''); setError(explain(problem) + '\nDữ liệu PO đã thay đổi. Tải lại để kiểm tra trạng thái mới.');
      } else setActionError(explain(problem) + (problem instanceof ApiError && (problem.status === 0 || problem.status >= 500) ? '\nHãy tải lại chi tiết đơn trước khi thử lại nếu chưa rõ kết quả thao tác.' : ''));
    } finally { inFlight.current = false; setBusy(false); }
  }
  const state = order?.status.toUpperCase();
  const totalReceived = order?.lines.reduce((sum, line) => sum + (line.receivedQuantity || 0), 0) || 0;
  return <PurchasingLayout>
    <Link className="po-back-link" to="/purchasing/purchase-orders">← Đơn mua hàng</Link>
    {error ? <PoError message={error} retry={() => setAttempt(value => value + 1)} /> : !order ? <div className="po-card po-loading" role="status">Đang tải chi tiết đơn mua…</div> : <>
      {(notice || query.get('created') || query.get('updated')) && <div className="po-success" role="status">✓ {notice || (query.get('updated') ? 'Đã lưu thay đổi bản nháp và Audit Log.' : 'Đã tạo bản nháp và lưu Audit Log.')}</div>}
      <div className="po-heading"><div><p className="workspace-eyebrow">CHI TIẾT ĐƠN MUA</p><h1>{order.poCode}</h1><p>Ngày lập {formatDate(order.orderDate)} · {order.lineCount} sản phẩm</p></div><DraftBadge status={order.status} /></div>
      <div className="po-detail-actions">
        {state === 'DRAFT' && <><Link className="utility-button" to={`/purchasing/purchase-orders/${id}/edit`}>Chỉnh sửa bản nháp</Link><button className="po-primary-button" onClick={() => start('confirm')}>Xác nhận PO</button></>}
        {(state === 'DRAFT' || state === 'CONFIRMED') && <button className="utility-button po-danger-text" disabled={order.hasAnyInbound} onClick={() => start('cancel')}>Hủy PO</button>}
        {state === 'CONFIRMED' && <button className="utility-button" disabled={order.openInboundCount > 0} onClick={() => start('close')}>Đóng PO</button>}
      </div>
      {order.hasAnyInbound && (state === 'DRAFT' || state === 'CONFIRMED') && <p className="po-bottom-note">Đơn đã có phiếu nhập nên không thể hủy.{order.openInboundCount > 0 && ` Còn ${order.openInboundCount} phiếu nhập mở; chưa thể đóng PO.`}</p>}
      <section className="po-card po-form-section"><h2>Thông tin đơn mua</h2><dl className="po-details"><div><dt>Nhà cung cấp</dt><dd>{order.supplierName}</dd></div><div><dt>Kho nhận</dt><dd>{order.warehouseName}</dd></div><div><dt>Ngày dự kiến giao</dt><dd>{formatDate(order.expectedArrivalDate)}</dd></div><div><dt>Người tạo</dt><dd>{order.createdByName || `Tài khoản #${order.createdByUserId}`}</dd></div>{order.confirmedAt && <div><dt>Đã xác nhận ngày</dt><dd>{formatDate(order.confirmedAt)}</dd></div>}{order.closedAt && <div><dt>{state === 'CANCELLED' ? 'Đã hủy ngày' : 'Đã đóng ngày'}</dt><dd>{formatDate(order.closedAt)}</dd></div>}<div className="po-details-wide"><dt>Ghi chú</dt><dd className="po-note">{order.note || 'Không có ghi chú.'}</dd></div>{order.closeReason && <div className="po-details-wide"><dt>Lý do {state === 'CANCELLED' ? 'hủy' : 'đóng'}</dt><dd className="po-note">{order.closeReason}</dd></div>}</dl></section>
      <section className="po-card po-form-section"><div className="section-heading"><h2>Sản phẩm đặt mua</h2><span className="po-muted">Đã nhận {totalReceived.toLocaleString('vi-VN')} / {order.totalQuantity.toLocaleString('vi-VN')}</span></div><div className="po-table-wrap"><table className="po-table"><thead><tr><th>Mã sản phẩm</th><th>Tên sản phẩm</th><th>Đơn vị</th><th className="po-number">Đặt mua</th><th className="po-number">Đã nhận</th><th className="po-number">Đóng thiếu</th><th className="po-number">Còn nhận</th></tr></thead><tbody>{order.lines.map(line => <tr key={line.poLineId}><td className="po-product-code">{line.productCode}</td><td>{line.productName}</td><td>{line.unit}</td><td className="po-number">{line.quantity.toLocaleString('vi-VN')}</td><td className="po-number">{(line.receivedQuantity || 0).toLocaleString('vi-VN')}</td><td className="po-number">{(line.closedQuantity || 0).toLocaleString('vi-VN')}</td><td className="po-number">{(line.remainingQuantity ?? line.quantity).toLocaleString('vi-VN')}</td></tr>)}</tbody></table></div></section>
      <p className="po-bottom-note">{state === 'DRAFT' ? 'Đơn nháp có thể chỉnh sửa. Xác nhận PO trước khi nhận hàng.' : state === 'CONFIRMED' ? 'Đơn đã xác nhận không thể chỉnh sửa. Hệ thống kiểm tra lại điều kiện nhập hàng khi hủy hoặc đóng.' : 'Đơn đã kết thúc và không thể chỉnh sửa.'}</p>
    </>}
    {action && order && <PoTransitionDialog action={action} code={order.poCode} reason={reason} busy={busy} error={actionError} onReason={setReason} onCancel={() => { if (!busy) setAction(null); }} onSubmit={submit} />}
  </PurchasingLayout>;
}
