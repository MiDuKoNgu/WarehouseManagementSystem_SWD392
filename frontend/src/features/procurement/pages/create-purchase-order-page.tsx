import { Link, useNavigate } from 'react-router-dom';
import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { purchaseOrderApi } from '../api/purchase-order-api';
import { PurchasingLayout } from '../components/purchasing-layout';
import { PoError } from '../components/po-common';
import { usePoError } from '../hooks/use-po-error';
import type { PurchaseOrderOptions } from '../types/purchase-order';
import { ApiError } from '../../../shared/lib/http-client';

interface EditableLine { key: number; productId: string; quantity: string }
export function CreatePurchaseOrderPage({ id }: { id?: number } = {}) {
  const navigate = useNavigate();
  const [options, setOptions] = useState<PurchaseOrderOptions | null>(null);
  const [supplierId, setSupplierId] = useState('');
  const [warehouseId, setWarehouseId] = useState('');
  const [arrival, setArrival] = useState('');
  const [note, setNote] = useState('');
  const [lines, setLines] = useState<EditableLine[]>([{ key: 1, productId: '', quantity: '1' }]);
  const nextKey = useRef(2);
  const savingRef = useRef(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [loadError, setLoadError] = useState('');
  const [attempt, setAttempt] = useState(0);
  const [rowVersion, setRowVersion] = useState('');
  const [poCode, setPoCode] = useState('');
  const explain = usePoError();
  useEffect(() => {
    let cancelled = false; setLoadError('');
    Promise.all([purchaseOrderApi.options(), id ? purchaseOrderApi.detail(id) : Promise.resolve(null)]).then(([data, order]) => {
      if (cancelled) return;
      if (order && order.status.toUpperCase() !== 'DRAFT') { setLoadError('Chỉ có thể chỉnh sửa PO ở trạng thái bản nháp.'); return; }
      setOptions(data);
      if (order) {
        setSupplierId(String(order.supplierId)); setWarehouseId(String(order.warehouseId));
        setArrival(order.expectedArrivalDate || ''); setNote(order.note || ''); setRowVersion(order.rowVersion); setPoCode(order.poCode);
        setLines(order.lines.map((line, index) => ({ key: index + 1, productId: String(line.productId), quantity: String(line.quantity) })));
        nextKey.current = order.lines.length + 1;
      }
    })
      .catch(reason => { if (!cancelled) setLoadError(explain(reason)); });
    return () => { cancelled = true; };
  }, [id, attempt, explain]);
  const products = useMemo(() => options?.products.filter(product => product.supplierIds.includes(Number(supplierId))) ?? [], [options, supplierId]);
  const total = lines.reduce((sum, line) => sum + (Number.isSafeInteger(Number(line.quantity)) && Number(line.quantity) > 0 ? Number(line.quantity) : 0), 0);
  function changeLine(key: number, change: Partial<EditableLine>) {
    setLines(current => current.map(line => line.key === key ? { ...line, ...change } : line)); setError('');
  }
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (savingRef.current) return; setError('');
    if (!supplierId || !warehouseId) { setError('Vui lòng chọn nhà cung cấp và kho nhận.'); return; }
    if (!lines.length || lines.some(line => !line.productId)) { setError('Vui lòng chọn sản phẩm cho tất cả các dòng.'); return; }
    if (lines.some(line => !/^\d+$/.test(line.quantity) || !Number.isSafeInteger(Number(line.quantity)) || Number(line.quantity) < 1 || Number(line.quantity) > 2147483647)) { setError('Số lượng mỗi dòng phải là số nguyên từ 1 đến 2.147.483.647.'); return; }
    if (new Set(lines.map(line => line.productId)).size !== lines.length) { setError('Một sản phẩm chỉ được xuất hiện một lần trong đơn.'); return; }
    if (lines.some(line => !products.some(product => product.productId === Number(line.productId)))) { setError('Sản phẩm phải thuộc nhà cung cấp đã chọn.'); return; }
    const today = new Date(); const localToday = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
    if (arrival && arrival < localToday) { setError('Ngày dự kiến giao không được trước ngày hôm nay.'); return; }
    savingRef.current = true; setSaving(true);
    try {
      const body = { supplierId: Number(supplierId), warehouseId: Number(warehouseId), expectedArrivalDate: arrival || null, note: note.trim() || null, lines: lines.map(line => ({ productId: Number(line.productId), quantity: Number(line.quantity) })) };
      const created = id ? await purchaseOrderApi.update(id, { ...body, rowVersion }) : await purchaseOrderApi.create(body);
      navigate(`/purchasing/purchase-orders/${created.purchaseOrderId}?${id ? 'updated=1' : 'created=manual'}`);
    } catch (reason) {
      const uncertain = reason instanceof ApiError && (reason.status === 0 || reason.status >= 500);
      setError(explain(reason) + (reason instanceof ApiError && reason.status === 409 ? '\nPO đã thay đổi. Tải lại dữ liệu trước khi tiếp tục sửa.' : '') + (uncertain ? '\nNếu kết nối bị ngắt trong lúc lưu, hãy kiểm tra chi tiết đơn trước khi lưu lại.' : ''));
      savingRef.current = false; setSaving(false);
    }
  }
  return <PurchasingLayout>
    <Link className="po-back-link" to="/purchasing/purchase-orders">← Đơn mua hàng</Link>
    <div className="po-heading"><div><p className="workspace-eyebrow">{id ? poCode || 'CHỈNH SỬA BẢN NHÁP' : 'BẢN NHÁP MỚI'}</p><h1>{id ? 'Chỉnh sửa đơn mua hàng' : 'Tạo đơn mua hàng'}</h1><p>Chọn nguồn hàng, kho nhận và những sản phẩm cần mua.</p></div><span className="quiet-badge">Lưu ở trạng thái nháp</span></div>
    {loadError ? <PoError message={loadError} retry={() => setAttempt(value => value + 1)} /> : !options ? <div className="po-card po-loading" role="status">Đang tải nhà cung cấp và sản phẩm…</div>
      : options.suppliers.length === 0 || options.warehouses.length === 0 || options.products.length === 0 ? <section className="po-empty"><h2>Chưa đủ dữ liệu để tạo đơn.</h2><p>Cần có nhà cung cấp, kho hoạt động và sản phẩm được gán cho nhà cung cấp. Vui lòng liên hệ quản trị viên để bổ sung dữ liệu.</p><button className="utility-button" onClick={() => setAttempt(value => value + 1)}>Tải lại dữ liệu</button></section>
      : <form onSubmit={save} aria-busy={saving}>
        <fieldset disabled={saving} className="po-fieldset">
          <section className="po-card po-form-section"><h2>Thông tin đơn mua</h2><div className="po-form-grid">
            <label>Nhà cung cấp <span className="required-mark">*</span><select required value={supplierId} onChange={event => { setSupplierId(event.target.value); setLines([{ key: nextKey.current++, productId: '', quantity: '1' }]); setError(''); }}><option value="">Chọn nhà cung cấp</option>{options.suppliers.map(supplier => <option value={supplier.supplierId} key={supplier.supplierId}>{supplier.supplierName}</option>)}</select></label>
            <label>Kho nhận <span className="required-mark">*</span><select required value={warehouseId} onChange={event => setWarehouseId(event.target.value)}><option value="">Chọn kho nhận</option>{options.warehouses.map(warehouse => <option value={warehouse.warehouseId} key={warehouse.warehouseId}>{warehouse.warehouseName} — {warehouse.address}</option>)}</select></label>
            <label>Ngày dự kiến giao<input type="date" value={arrival} onChange={event => setArrival(event.target.value)} /><small>Có thể bổ sung sau khi trao đổi với nhà cung cấp.</small></label>
            <label>Ghi chú<textarea value={note} onChange={event => setNote(event.target.value)} maxLength={1000} placeholder="Thông tin cần lưu ý cho đơn mua này…" rows={3} /><small>{note.length}/1000 ký tự</small></label>
          </div></section>
          <section className="po-card po-form-section"><div className="section-heading"><h2>Sản phẩm đặt mua</h2><span className="po-muted">{lines.length} dòng sản phẩm</span></div>
            {!supplierId ? <p className="po-helper">Chọn nhà cung cấp để xem sản phẩm có thể đặt mua.</p> : products.length === 0 ? <p className="po-helper">Nhà cung cấp này chưa có sản phẩm hợp lệ. Hãy chọn nhà cung cấp khác.</p> : null}
            <div className="po-table-wrap"><table className="po-table po-edit-table"><thead><tr><th>Sản phẩm</th><th>Đơn vị</th><th>Số lượng <span className="required-mark">*</span></th><th><span className="sr-only">Thao tác</span></th></tr></thead><tbody>{lines.map((line, index) => {
              const selected = products.find(product => product.productId === Number(line.productId));
              return <tr key={line.key}><td><select aria-label={`Sản phẩm dòng ${index + 1}`} required disabled={!supplierId || products.length === 0} value={line.productId} onChange={event => changeLine(line.key, { productId: event.target.value })}><option value="">Chọn sản phẩm</option>{products.map(product => <option key={product.productId} value={product.productId} disabled={lines.some(other => other.key !== line.key && other.productId === String(product.productId))}>{product.productCode} — {product.productName}</option>)}</select></td><td>{selected?.unit || '—'}</td><td><input aria-label={`Số lượng dòng ${index + 1}`} type="number" min={1} max={2147483647} step={1} required value={line.quantity} onChange={event => changeLine(line.key, { quantity: event.target.value })} /></td><td><button type="button" className="po-remove-button" aria-label={`Xóa dòng ${index + 1}`} disabled={lines.length === 1} onClick={() => setLines(current => current.filter(item => item.key !== line.key))}>×</button></td></tr>;
            })}</tbody></table></div>
            <div className="po-lines-footer"><button type="button" className="utility-button" disabled={!supplierId || products.length === 0 || lines.length >= products.length || lines.length >= 500} onClick={() => setLines(current => [...current, { key: nextKey.current++, productId: '', quantity: '1' }])}>+ Thêm sản phẩm</button><span>Tổng số lượng <strong>{total.toLocaleString('vi-VN')}</strong></span></div>
          </section>
          {error && <PoError message={error} retry={id ? () => setAttempt(value => value + 1) : undefined} />}
          <div className="po-submit-bar"><p>Đơn được lưu ở trạng thái <strong>Bản nháp</strong>.</p><div className="po-actions"><Link className="utility-button" to="/purchasing/purchase-orders" aria-disabled={saving} onClick={event => { if (saving) event.preventDefault(); }}>Hủy</Link><button className="po-primary-button" type="submit" disabled={saving || !supplierId || products.length === 0}>{saving ? 'Đang lưu…' : 'Lưu bản nháp →'}</button></div></div>
        </fieldset>
      </form>}
  </PurchasingLayout>;
}
