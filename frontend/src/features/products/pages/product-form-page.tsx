import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { PurchasingLayout } from '../../procurement/components/purchasing-layout';
import { PoError } from '../../procurement/components/po-common';
import { usePoError } from '../../procurement/hooks/use-po-error';
import { ApiError } from '../../../shared/lib/http-client';
import { productApi } from '../api/product-api';
import type { ProductDetail, ProductOptions } from '../types/product';

const initial = { productCode: '', productName: '', model: '', unit: 'Chiếc', categoryId: '', brandId: '', status: 'ACTIVE', specifications: '', lengthCm: '', widthCm: '', heightCm: '', grossWeightKg: '' };
type ProductForm = typeof initial;
function validDecimal(value: string, places: number, max: number) { return new RegExp(`^\\d+(\\.\\d{1,${places}})?$`).test(value) && Number(value) > 0 && Number(value) <= max; }

export function ProductFormPage({ id }: { id?: number } = {}) {
  const navigate = useNavigate(); const explain = usePoError();
  const [form, setForm] = useState<ProductForm>(initial); const [suppliers, setSuppliers] = useState<number[]>([]);
  const [options, setOptions] = useState<ProductOptions | null>(null); const [current, setCurrent] = useState<ProductDetail | null>(null);
  const [error, setError] = useState(''); const [loadError, setLoadError] = useState(''); const [attempt, setAttempt] = useState(0);
  const [saving, setSaving] = useState(false); const pending = useRef(false);
  useEffect(() => {
    let cancelled = false; setLoadError(''); setOptions(null);
    Promise.all([productApi.options(), id ? productApi.detail(id) : Promise.resolve(null)]).then(([lookups, product]) => {
      if (cancelled) return; setOptions(lookups); setCurrent(product);
      if (product) {
        setForm({ productCode: product.productCode, productName: product.productName, model: product.model, unit: product.unit, categoryId: String(product.categoryId), brandId: String(product.brandId), status: product.status, specifications: product.specifications || '', lengthCm: String(product.lengthCm), widthCm: String(product.widthCm), heightCm: String(product.heightCm), grossWeightKg: String(product.grossWeightKg) });
        setSuppliers(product.suppliers.map(item => item.supplierId));
      }
    }).catch(reason => { if (!cancelled) setLoadError(explain(reason)); });
    return () => { cancelled = true; };
  }, [id, attempt, explain]);
  const change = (key: keyof ProductForm, value: string) => { setForm(previous => ({ ...previous, [key]: value })); setError(''); };
  async function submit(event: FormEvent) {
    event.preventDefault(); if (pending.current) return; setError('');
    if (!/^[A-Za-z0-9][A-Za-z0-9._/-]{0,39}$/.test(form.productCode.trim())) { setError('Mã sản phẩm gồm 1–40 ký tự chữ Latin, số, dấu chấm, gạch dưới, gạch ngang hoặc /.'); return; }
    if (!form.productName.trim() || !form.model.trim() || !form.unit.trim() || !form.categoryId || !form.brandId) { setError('Vui lòng nhập đủ tên, model, đơn vị, danh mục và hãng.'); return; }
    if (!suppliers.length) { setError('Vui lòng chọn ít nhất một nhà cung cấp.'); return; }
    if (!['lengthCm', 'widthCm', 'heightCm'].every(key => validDecimal(form[key as keyof ProductForm], 2, 99999999.99))) { setError('Dài, rộng, cao phải lớn hơn 0, tối đa 99.999.999,99 cm và không quá 2 chữ số thập phân.'); return; }
    if (!validDecimal(form.grossWeightKg, 3, 9999999.999)) { setError('Khối lượng phải lớn hơn 0, tối đa 9.999.999,999 kg và không quá 3 chữ số thập phân.'); return; }
    pending.current = true; setSaving(true);
    try {
      const product = await productApi.save({ productCode: form.productCode.trim().toUpperCase(), productName: form.productName.trim(), model: form.model.trim(), unit: form.unit.trim(), categoryId: Number(form.categoryId), brandId: Number(form.brandId), status: form.status, specifications: form.specifications.trim() || null, lengthCm: Number(form.lengthCm), widthCm: Number(form.widthCm), heightCm: Number(form.heightCm), grossWeightKg: Number(form.grossWeightKg), supplierIds: suppliers, ...(id ? { rowVersion: current?.rowVersion } : {}) }, id);
      navigate(`/purchasing/products/${product.productId}?saved=1`);
    } catch (reason) {
      setError(explain(reason) + (reason instanceof ApiError && reason.status === 409 ? '\nNếu dữ liệu đã được người khác sửa, hãy tải lại trước khi lưu.' : '') + (reason instanceof ApiError && (reason.status === 0 || reason.status >= 500) ? '\nKiểm tra danh sách sản phẩm trước khi gửi lại nếu chưa rõ kết quả lưu.' : ''));
      pending.current = false; setSaving(false);
    }
  }
  const supplierOptions = options ? [...options.suppliers, ...(current?.suppliers.filter(item => !options.suppliers.some(value => value.supplierId === item.supplierId)).map(item => ({ ...item, supplierName: `${item.supplierName} (không hoạt động)` })) || [])] : [];
  return <PurchasingLayout current="products"><Link className="po-back-link" to={id ? `/purchasing/products/${id}` : '/purchasing/products'}>← {id ? 'Chi tiết sản phẩm' : 'Sản phẩm'}</Link>
    <div className="po-heading"><div><p className="workspace-eyebrow">DANH MỤC THIẾT BỊ</p><h1>{id ? 'Chỉnh sửa sản phẩm' : 'Tạo sản phẩm'}</h1><p>Thông tin sản phẩm và nguồn cung được lưu cùng nhau.</p></div></div>
    {loadError ? <PoError message={loadError} retry={() => setAttempt(value => value + 1)} /> : !options ? <div className="po-card po-loading" role="status">Đang tải dữ liệu sản phẩm…</div> : !options.categories.length || !options.brands.length || !options.suppliers.length ? <section className="po-empty"><h2>Chưa đủ danh mục để tạo sản phẩm.</h2><p>Cần có danh mục, hãng và nhà cung cấp đang hoạt động.</p></section> : <form onSubmit={submit} aria-busy={saving}><fieldset disabled={saving} className="po-fieldset">
      <section className="po-card po-form-section"><h2>Thông tin cơ bản</h2><div className="po-form-grid">
        <label>Mã sản phẩm<input value={form.productCode} onChange={event => change('productCode', event.target.value)} readOnly={Boolean(id)} required maxLength={40} />{id && <small>Mã được giữ cố định sau khi tạo.</small>}</label>
        <label>Tên sản phẩm<input value={form.productName} onChange={event => change('productName', event.target.value)} required maxLength={200} /></label>
        <label>Model<input value={form.model} onChange={event => change('model', event.target.value)} required maxLength={100} /></label>
        <label>Đơn vị<input value={form.unit} onChange={event => change('unit', event.target.value)} required maxLength={30} /></label>
        <label>Danh mục<select value={form.categoryId} onChange={event => change('categoryId', event.target.value)} required><option value="">Chọn danh mục</option>{options.categories.map(item => <option key={item.categoryId} value={item.categoryId}>{item.categoryName}</option>)}</select></label>
        <label>Hãng<select value={form.brandId} onChange={event => change('brandId', event.target.value)} required><option value="">Chọn hãng</option>{options.brands.map(item => <option key={item.brandId} value={item.brandId}>{item.brandName}</option>)}{current && !options.brands.some(item => item.brandId === current.brandId) && <option value={current.brandId}>{current.brandName} (không hoạt động)</option>}</select></label>
        <label>Trạng thái<select value={form.status} onChange={event => change('status', event.target.value)}><option value="ACTIVE">Đang hoạt động</option><option value="INACTIVE">Ngừng hoạt động</option></select></label>
      </div></section>
      <section className="po-card po-form-section"><h2>Kích thước & khối lượng</h2><p className="po-bottom-note">Nhập số dương, kích thước tối đa 2 chữ số thập phân; khối lượng tối đa 3 chữ số.</p><div className="product-measure-grid">{(['lengthCm', 'widthCm', 'heightCm', 'grossWeightKg'] as const).map((key, index) => <label key={key}>{['Chiều dài (cm)', 'Chiều rộng (cm)', 'Chiều cao (cm)', 'Khối lượng (kg)'][index]}<input type="number" required min={index === 3 ? '0.001' : '0.01'} max={index === 3 ? '9999999.999' : '99999999.99'} step={index === 3 ? '0.001' : '0.01'} value={form[key]} onChange={event => change(key, event.target.value)} /></label>)}</div></section>
      <section className="po-card po-form-section"><h2>Nhà cung cấp</h2><p className="po-bottom-note">Chọn ít nhất một nhà cung cấp để sản phẩm có thể được đặt mua.</p><div className="product-suppliers">{supplierOptions.map(item => <label key={item.supplierId}><input type="checkbox" checked={suppliers.includes(item.supplierId)} onChange={event => setSuppliers(previous => event.target.checked ? [...previous, item.supplierId] : previous.filter(value => value !== item.supplierId))} /><span>{item.supplierName}</span></label>)}</div></section>
      <section className="po-card po-form-section"><label className="product-spec-label">Thông số kỹ thuật<textarea rows={5} maxLength={10000} value={form.specifications} onChange={event => change('specifications', event.target.value)} placeholder="Công suất, dung tích, tính năng hoặc mô tả kỹ thuật…" /></label><p className="po-bottom-note">{form.specifications.length}/10000 ký tự</p></section>
      {error && <PoError message={error} retry={id ? () => { setError(''); setAttempt(value => value + 1); } : undefined} />}
      <div className="po-submit-bar"><p>{id ? 'Giữ nguyên mã và người tạo sản phẩm.' : 'Sản phẩm hoạt động sẽ xuất hiện trong danh sách tạo PO của nhà cung cấp đã chọn.'}</p><div className="po-actions"><Link className="utility-button" to={id ? `/purchasing/products/${id}` : '/purchasing/products'} onClick={event => { if (saving) event.preventDefault(); }}>Hủy</Link><button type="submit" className="po-primary-button" disabled={saving}>{saving ? 'Đang lưu…' : 'Lưu sản phẩm →'}</button></div></div>
    </fieldset></form>}
  </PurchasingLayout>;
}
