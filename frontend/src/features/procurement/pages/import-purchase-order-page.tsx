import { Link, useNavigate } from 'react-router-dom';
import { useRef, useState, type FormEvent } from 'react';
import { purchaseOrderApi } from '../api/purchase-order-api';
import { PurchasingLayout } from '../components/purchasing-layout';
import { formatDate, PoError } from '../components/po-common';
import { usePoError } from '../hooks/use-po-error';
import type { ImportPreview } from '../types/purchase-order';

export function ImportPurchaseOrderPage() {
  const navigate = useNavigate();
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState<'preview' | 'confirm' | 'template' | null>(null);
  const operation = useRef(false);
  const explain = usePoError();
  async function download() {
    if (operation.current) return; operation.current = true; setBusy('template'); setError('');
    try { await purchaseOrderApi.template(); } catch (reason) { setError(explain(reason)); }
    finally { operation.current = false; setBusy(null); }
  }
  async function upload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!file || operation.current) return;
    setError(''); setPreview(null);
    if (!file.name.toLowerCase().endsWith('.xlsx')) { setError('Chỉ hỗ trợ file Excel .xlsx. Hãy dùng file mẫu bên dưới.'); return; }
    if (file.size > 2 * 1024 * 1024) { setError('File không được lớn hơn 2 MB.'); return; }
    operation.current = true; setBusy('preview');
    try { setPreview(await purchaseOrderApi.preview(file)); }
    catch (reason) { setError(explain(reason)); }
    finally { operation.current = false; setBusy(null); }
  }
  async function confirm() {
    if (!preview?.valid || !preview.token || operation.current) return;
    operation.current = true; setBusy('confirm'); setError('');
    try {
      const order = await purchaseOrderApi.confirm(preview.token);
      navigate(`/purchasing/purchase-orders/${order.purchaseOrderId}?created=excel`);
    } catch (reason) { setError(explain(reason)); operation.current = false; setBusy(null); }
  }
  const errorRows = preview?.rows.filter(row => row.errors.length > 0).length ?? 0;
  return <PurchasingLayout current="import">
    <Link className="po-back-link" to="/purchasing/purchase-orders">← Đơn mua hàng</Link>
    <div className="po-heading"><div><p className="workspace-eyebrow">NHẬP LIỆU TỪ EXCEL</p><h1>Một file. Một đơn mua.</h1><p>Tải mẫu, điền dữ liệu và kiểm tra trước khi tạo bản nháp.</p></div><span className="po-import-sticker" aria-hidden="true">↥</span></div>
    <div className="po-import-guide"><div><strong>Chuẩn bị file của bạn</strong><p>Sheet <b>PurchaseOrder</b> gồm SupplierID, WarehouseID, ExpectedArrivalDate, Note, ProductCode và Quantity. Tra mã hợp lệ ở các sheet tham chiếu trong file mẫu.</p></div><button type="button" className="utility-button" disabled={busy !== null} onClick={download}>{busy === 'template' ? 'Đang tải…' : '↓ Tải file mẫu'}</button></div>
    <form className="po-card po-upload-section" onSubmit={upload} aria-busy={busy === 'preview'}><label htmlFor="po-import-file" className="po-upload-label"><span aria-hidden="true">▤</span><strong>Chọn file Excel để kiểm tra</strong><span>Định dạng .xlsx · Tối đa 2 MB · Tối đa 500 dòng</span></label><input id="po-import-file" type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" disabled={busy !== null} onChange={event => { setFile(event.target.files?.[0] ?? null); setPreview(null); setError(''); }} /><div className="po-upload-footer"><span>{file ? file.name : 'Chưa chọn file'}</span><button type="submit" className="po-primary-button" disabled={!file || busy !== null}>{busy === 'preview' ? 'Đang kiểm tra…' : 'Kiểm tra & xem trước →'}</button></div></form>
    <p className="po-bottom-note">Các dòng phải cùng nhà cung cấp, kho nhận, ngày giao và ghi chú. Dòng trống hoàn toàn được bỏ qua; sản phẩm trùng hoặc số lượng không hợp lệ sẽ được đánh dấu.</p>
    {error && <PoError message={error} />}
    {preview && <section className="po-preview-section" aria-labelledby="preview-title">
      <div className="section-heading"><h2 id="preview-title">Xem trước dữ liệu</h2><span className={`po-preview-result ${preview.valid ? 'is-valid' : ''}`}>{preview.valid ? '✓ Dữ liệu hợp lệ' : errorRows > 0 ? `${errorRows} dòng có lỗi` : 'Cần kiểm tra dữ liệu'}</span></div>
      {preview.errors.length > 0 && <div className="po-alert" role="alert"><strong>Cần kiểm tra lại file</strong><ul>{preview.errors.map((item, index) => <li key={index}>{item}</li>)}</ul></div>}
      <div className="po-card po-table-wrap"><table className="po-table po-preview-table"><thead><tr><th>Dòng</th><th>NCC / Kho</th><th>Sản phẩm</th><th className="po-number">Số lượng</th><th>Ngày giao</th><th>Ghi chú</th><th>Kết quả</th></tr></thead><tbody>{preview.rows.map(row => <tr key={row.rowNumber} className={row.errors.length > 0 ? 'po-row-invalid' : ''}><td>{row.rowNumber}</td><td>{row.supplierId ?? '—'} / {row.warehouseId ?? '—'}</td><td><strong>{row.productCode || '—'}</strong>{row.productName && <small>{row.productName}</small>}</td><td className="po-number">{row.quantity?.toLocaleString('vi-VN') ?? '—'}</td><td>{formatDate(row.expectedArrivalDate)}</td><td className="po-preview-note">{row.note || '—'}</td><td>{row.errors.length ? <ul className="po-row-errors">{row.errors.map((item, index) => <li key={index}>{item}</li>)}</ul> : <span className="po-row-valid">Hợp lệ</span>}</td></tr>)}</tbody></table>{preview.rows.length === 0 && <p className="po-no-results">File chưa có dòng sản phẩm để nhập.</p>}</div>
      <div className="po-submit-bar"><p>{preview.valid ? 'Dữ liệu chưa được lưu. Xác nhận để tạo PO nháp và Audit Log.' : 'Sửa các lỗi trong file Excel, sau đó tải lên kiểm tra lại.'}</p><button type="button" className="po-primary-button" onClick={confirm} disabled={!preview.valid || !preview.token || busy !== null}>{busy === 'confirm' ? 'Đang tạo đơn…' : 'Xác nhận import →'}</button></div>
      {preview.valid && <p className="po-bottom-note">Bản xem trước có hiệu lực trong 15 phút. Dữ liệu được kiểm tra lại khi xác nhận.</p>}
    </section>}
  </PurchasingLayout>;
}
