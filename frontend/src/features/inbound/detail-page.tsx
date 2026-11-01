import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { inboundApi } from './api';
import type { ReceiptDetail, ReceiptItem, SaveReceiptItem } from './types';
import { date, ErrorNotice, InboundLayout, message, Status } from './layout';
import { Progress } from './progress';
import { SerialForm } from './serial-form';

export function InboundDetailPage({ id }: { id: number }) {
  const [data, setData] = useState<ReceiptDetail | null>(null); const [error, setError] = useState(''); const [success, setSuccess] = useState('');
  const [busy, setBusy] = useState(false); const [loading, setLoading] = useState(true); const [attempt, setAttempt] = useState(0);
  const [editing, setEditing] = useState<ReceiptItem | null>(null); const [action, setAction] = useState<'complete' | 'cancel' | null>(null); const [removing, setRemoving] = useState<ReceiptItem | null>(null);
  useEffect(() => { let active = true; setLoading(true); setError(''); inboundApi.detail(id).then(value => { if (active) { setData(value); setEditing(null); } }).catch(e => { if (active) setError(message(e)); }).finally(() => { if (active) setLoading(false); }); return () => { active = false; }; }, [id, attempt]);
  async function save(body: SaveReceiptItem) {
    setBusy(true); setError(''); setSuccess('');
    try { const value = await inboundApi.saveItem(id, editing?.inboundItemId ?? null, body); setData(value); setEditing(null); setSuccess(editing ? 'Đã cập nhật Serial. Lịch sử thay đổi đã được lưu.' : 'Đã thêm Serial. Kiểm tra thông tin để tiếp tục quét.'); return null; }
    catch (e) { return message(e); } finally { setBusy(false); }
  }
  async function finish(reason: string) {
    if (!data || !action || busy) return; setBusy(true); setError(''); setSuccess('');
    try { const value = await inboundApi.transition(id, action, data.receipt.rowVersion, reason); setData(value); setAction(null); setEditing(null); setSuccess(action === 'complete' ? 'Đã hoàn tất phiếu. Các thiết bị đang chờ Putaway, chưa được gán vị trí.' : 'Đã hủy phiếu nhập.'); }
    catch (e) { setError(message(e)); } finally { setBusy(false); }
  }
  async function remove() {
    if (!data || !removing || busy) return; setBusy(true); setError(''); setSuccess('');
    try { setData(await inboundApi.removeItem(id, removing.inboundItemId, data.receipt.rowVersion)); if (editing?.inboundItemId === removing.inboundItemId) setEditing(null); setRemoving(null); setSuccess('Đã hủy hiệu lực Serial. Lịch sử tiếp nhận được giữ lại.'); }
    catch (e) { setError(message(e)); } finally { setBusy(false); }
  }
  async function synchronize() {
    if (!data || busy) return; setBusy(true); setError(''); setSuccess('');
    try { setData(await inboundApi.transition(id, data.receipt.status === 'CANCELLED' ? 'cancel' : 'complete', data.receipt.rowVersion)); setSuccess('Đã đồng bộ trạng thái nhận hàng với PO.'); }
    catch (e) { setError(message(e)); } finally { setBusy(false); }
  }
  const draft = data?.receipt.status === 'DRAFT';
  return <InboundLayout><Link className="po-back-link" to="/warehouse/inbounds">← Phiếu nhập kho</Link><ErrorNotice error={error} retry={() => { setAction(null); setRemoving(null); setAttempt(a => a + 1); }} />{success && <p className="po-success" role="status">{success}</p>}
    {loading ? <p role="status">Đang tải chi tiết phiếu…</p> : data && <><div className="po-heading"><div><p className="workspace-eyebrow">PHIẾU NHẬP KHO</p><h1>{data.receipt.receiptCode}</h1><Status value={data.receipt.status} /></div>{draft && <div className="po-actions">{data.policy.allowCancellation && <button className="utility-button" disabled={busy} onClick={() => setAction('cancel')}>Hủy phiếu</button>}<button className="po-primary-button" disabled={busy || !data.items.length || !!editing} onClick={() => setAction('complete')}>Hoàn tất phiếu nhập</button></div>}{!draft && <button className="utility-button" disabled={busy} onClick={() => void synchronize()}>Đồng bộ trạng thái PO</button>}</div>
      <section className="po-card po-form-section"><dl className="po-details"><div><dt>Purchase Order / Nhà cung cấp</dt><dd>{data.order.poCode} · {data.order.supplierName}</dd></div><div><dt>Kho nhận</dt><dd>{data.order.warehouseName}</dd></div><div><dt>Người tạo / Thời gian</dt><dd>{data.receipt.createdByName || `Người dùng #${data.receipt.createdByUserId}`}<small className="inbound-block po-muted">{date(data.receipt.createdAt)}</small></dd></div><div><dt>Ngày nhận</dt><dd>{data.receipt.receiptDate}</dd></div>{data.receipt.completedAt && <div><dt>Người hoàn tất / Thời gian</dt><dd>{data.receipt.performedByName || `Người dùng #${data.receipt.performedByUserId}`}<small className="inbound-block po-muted">{date(data.receipt.completedAt)}</small></dd></div>}{data.receipt.note && <div className="po-details-wide"><dt>Ghi chú</dt><dd className="po-note">{data.receipt.note}</dd></div>}</dl></section>
      <Progress lines={data.lines} />{!draft && data.receipt.status !== 'CANCELLED' && <p className="po-helper"><Link className="po-code-link" to="/warehouse/pending-putaway">Xem thiết bị chờ Putaway →</Link></p>}<div className={`inbound-detail-grid ${!draft ? 'inbound-readonly' : ''}`}><section className="inbound-section"><h2>Serial đã tiếp nhận <span className="quiet-badge">{data.items.length}</span></h2><p className="po-muted">{draft ? 'Serial trong DRAFT chưa được ghi nhận vào tồn kho chính thức.' : 'Phiếu đã khóa. Lịch sử và thông tin sản xuất được giữ theo Serial.'}</p>{!data.items.length && <div className="po-empty"><h3>Bắt đầu từ serial đầu tiên</h3><p>Chọn sản phẩm, tháng/năm sản xuất rồi quét camera, tải ảnh tem hoặc nhập tay.</p></div>}<div className="inbound-items">{data.items.map(item => <article className={`po-card inbound-item ${editing?.inboundItemId === item.inboundItemId ? 'inbound-item-editing' : ''}`} key={item.inboundItemId}><strong className="inbound-serial">{item.scannedSerial}</strong><p>{data.order.lines.find(l => l.poLineId === item.poLineId)?.productName || `Dòng PO #${item.poLineId}`}</p><dl><div><dt>Sản xuất</dt><dd>{item.manufacturingMonth ?? '—'}/{item.manufacturingYear ?? '—'}</dd></div><div><dt>Tình trạng</dt><dd>{item.receivedCondition === 'DAMAGED' ? 'Hỏng · cách ly' : item.receivedCondition === 'GOOD' ? 'Tốt' : '—'}</dd></div><div><dt>Phương thức</dt><dd>{item.inputMethod === 'CAMERA' ? 'Camera' : item.inputMethod === 'IMAGE_UPLOAD' ? 'Ảnh tem' : item.inputMethod === 'MANUAL' ? 'Nhập tay' : 'Chưa ghi nhận (dữ liệu cũ)'}</dd></div><div><dt>Người nhập</dt><dd>{item.scannedByName || (item.scannedByUserId ? `Người dùng #${item.scannedByUserId}` : 'Chưa ghi nhận (dữ liệu cũ)')}</dd></div></dl>{item.conditionNote && <p className="po-note">{item.conditionNote}</p>}<small className="po-muted">{date(item.scannedAt)}</small>{draft && <div className="po-actions"><button className="utility-button" disabled={busy} onClick={() => { setEditing(item); setSuccess(''); }}>Sửa Serial</button><button className="utility-button" disabled={busy} onClick={() => setRemoving(item)}>Hủy Serial</button></div>}</article>)}</div></section>
      {draft && <SerialForm data={data} editing={editing} busy={busy} save={save} cancel={() => setEditing(null)} />}</div>
    </>}
    {action && data && <ConfirmDialog title={action === 'complete' ? 'Hoàn tất phiếu nhập?' : 'Hủy phiếu nhập?'} busy={busy} reasonRequired={action === 'cancel'} onCancel={() => setAction(null)} onConfirm={reason => void finish(reason)}><p>{action === 'complete' ? `Xác nhận ${data.items.length} serial. Sau khi hoàn tất, phiếu không thể sửa; thiết bị chuyển sang chờ Putaway.` : 'Các Serial nháp sẽ được hủy hiệu lực và lưu dấu vết. Phiếu không thể tiếp tục tiếp nhận.'}</p><ErrorNotice error={error} /></ConfirmDialog>}
    {removing && <ConfirmDialog title="Hủy Serial nhập sai?" busy={busy} onCancel={() => setRemoving(null)} onConfirm={() => void remove()}><p className="inbound-serial">{removing.scannedSerial}</p><p>Hủy hiệu lực bản ghi nháp và giữ lịch sử thao tác.</p><ErrorNotice error={error} /></ConfirmDialog>}
  </InboundLayout>;
}

function ConfirmDialog({ title, children, reasonRequired = false, busy, onCancel, onConfirm }: { title: string; children: React.ReactNode; reasonRequired?: boolean; busy: boolean; onCancel: () => void; onConfirm: (reason: string) => void }) {
  const [reason, setReason] = useState(''); const dialog = useRef<HTMLDivElement>(null); const [error, setError] = useState('');
  useEffect(() => { const old = document.activeElement as HTMLElement | null; dialog.current?.focus(); return () => old?.focus(); }, []);
  return <div className="inbound-modal-backdrop"><div ref={dialog} role="dialog" aria-modal="true" aria-label={title} tabIndex={-1} className="po-card inbound-modal" onKeyDown={e => {
    if (e.key === 'Escape' && !busy) onCancel();
    if (e.key === 'Tab') { const nodes = dialog.current?.querySelectorAll<HTMLElement>('button:not(:disabled),textarea:not(:disabled)'); if (nodes?.length) { const first = nodes[0]; const last = nodes[nodes.length - 1]; if (e.shiftKey && (document.activeElement === first || document.activeElement === dialog.current)) { e.preventDefault(); last.focus(); } else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); } } }
  }}><h2>{title}</h2>{children}<form onSubmit={e => { e.preventDefault(); if (busy) return; if (reasonRequired && !reason.trim()) { setError('Vui lòng nhập lý do hủy phiếu.'); return; } onConfirm(reason.trim()); }}>{reasonRequired && <label>Lý do hủy<textarea required maxLength={1000} value={reason} onChange={e => setReason(e.target.value)} disabled={busy} /></label>}<ErrorNotice error={error} /><div className="po-actions"><button className="utility-button" type="button" disabled={busy} onClick={onCancel}>Quay lại</button><button className="po-primary-button" disabled={busy}>{busy ? 'Đang xử lý…' : 'Xác nhận'}</button></div></form></div></div>;
}

