import { useEffect, useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { PurchasingLayout } from '../../procurement/components/purchasing-layout';
import { PoError } from '../../procurement/components/po-common';
import { usePoError } from '../../procurement/hooks/use-po-error';
import { productApi } from '../api/product-api';
import { ProductStatus } from '../components/product-status';
import type { ProductOptions, ProductPage } from '../types/product';

const empty = { search: '', categoryId: '', brandId: '', supplierId: '', status: '' };
function filters(search: string) { const query = new URLSearchParams(search); return Object.fromEntries(Object.keys(empty).map(key => [key, query.get(key) || ''])) as typeof empty; }

export function ProductListPage() {
  const location = useLocation(); const navigate = useNavigate(); const explain = usePoError();
  const [draft, setDraft] = useState(() => filters(location.search));
  const [options, setOptions] = useState<ProductOptions>({ categories: [], brands: [], suppliers: [] });
  const [data, setData] = useState<ProductPage | null>(null); const [error, setError] = useState('');
  const [optionsError, setOptionsError] = useState(''); const [loading, setLoading] = useState(true); const [attempt, setAttempt] = useState(0);
  useEffect(() => { setDraft(filters(location.search)); }, [location.search]);
  useEffect(() => {
    let cancelled = false; setOptionsError('');
    productApi.options().then(value => { if (!cancelled) setOptions(value); }).catch(reason => { if (!cancelled) setOptionsError(explain(reason)); });
    return () => { cancelled = true; };
  }, [attempt, explain]);
  useEffect(() => {
    let cancelled = false; setLoading(true); setError('');
    const query = new URLSearchParams(location.search); query.set('pageSize', '20');
    productApi.list(query.toString()).then(value => { if (!cancelled) setData(value); }).catch(reason => { if (!cancelled) setError(explain(reason)); }).finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [location.search, attempt, explain]);
  function apply(event: FormEvent) { event.preventDefault(); const query = new URLSearchParams(); Object.entries(draft).forEach(([key, value]) => { if (value.trim()) query.set(key, value.trim()); }); navigate({ pathname: '/purchasing/products', search: query.toString() }); }
  function page(value: number) { const query = new URLSearchParams(location.search); query.set('page', String(value)); navigate({ search: query.toString() }); }
  const change = (key: keyof typeof empty, value: string) => setDraft(current => ({ ...current, [key]: value }));
  return <PurchasingLayout current="products">
    <div className="po-heading"><div><p className="workspace-eyebrow">DANH MỤC THIẾT BỊ</p><h1>Sản phẩm</h1><p>Thông tin rõ ràng cho mỗi lần đặt mua và vận hành kho.</p></div><Link className="po-primary-button" to="/purchasing/products/new">+ Tạo sản phẩm</Link></div>
    <form className="po-card product-filters" onSubmit={apply}><label className="product-search">Tìm kiếm<input type="search" maxLength={100} placeholder="Mã, tên hoặc model sản phẩm…" value={draft.search} onChange={event => change('search', event.target.value)} /></label><div className="product-filter-grid"><label>Danh mục<select value={draft.categoryId} onChange={event => change('categoryId', event.target.value)}><option value="">Tất cả danh mục</option>{options.categories.map(item => <option key={item.categoryId} value={item.categoryId}>{item.categoryName}</option>)}</select></label><label>Hãng<select value={draft.brandId} onChange={event => change('brandId', event.target.value)}><option value="">Tất cả hãng</option>{options.brands.map(item => <option key={item.brandId} value={item.brandId}>{item.brandName}</option>)}</select></label><label>Nhà cung cấp<select value={draft.supplierId} onChange={event => change('supplierId', event.target.value)}><option value="">Tất cả nhà cung cấp</option>{options.suppliers.map(item => <option key={item.supplierId} value={item.supplierId}>{item.supplierName}</option>)}</select></label><label>Trạng thái<select value={draft.status} onChange={event => change('status', event.target.value)}><option value="">Tất cả trạng thái</option><option value="ACTIVE">Đang hoạt động</option><option value="INACTIVE">Ngừng hoạt động</option></select></label></div><div className="product-filter-actions"><button type="button" className="utility-button" onClick={() => navigate('/purchasing/products')}>Đặt lại</button><button className="po-primary-button" type="submit">Áp dụng bộ lọc</button></div></form>
    {optionsError && <PoError message={`Chưa tải được danh sách bộ lọc: ${optionsError}`} retry={() => setAttempt(value => value + 1)} />}
    {error ? <PoError message={error} retry={() => setAttempt(value => value + 1)} /> : loading ? <div className="po-card po-loading" role="status">Đang tải sản phẩm…</div> : data && <>
      <div className="po-toolbar"><span>{data.totalCount.toLocaleString('vi-VN')} sản phẩm</span><span className="po-muted">Trang {data.page}</span></div>
      <div className="po-card po-table-wrap"><table className="po-table"><thead><tr><th>Mã / Tên sản phẩm</th><th>Danh mục</th><th>Hãng</th><th>Model</th><th>Đơn vị</th><th>Trạng thái</th></tr></thead><tbody>{data.items.map(item => <tr key={item.productId}><td><Link className="po-code-link" to={`/purchasing/products/${item.productId}`}>{item.productCode}</Link><small>{item.productName}</small></td><td>{item.categoryName}</td><td>{item.brandName}</td><td>{item.model}</td><td>{item.unit}</td><td><ProductStatus status={item.status} /></td></tr>)}</tbody></table>{data.items.length === 0 && <div className="po-empty"><h2>Chưa có sản phẩm phù hợp.</h2><p>Điều chỉnh bộ lọc hoặc tạo sản phẩm mới để bắt đầu.</p></div>}</div>
      <div className="product-pagination"><button className="utility-button" disabled={data.page <= 1} onClick={() => page(data.page - 1)}>← Trang trước</button><span>Trang {data.page} / {Math.max(1, Math.ceil(data.totalCount / data.pageSize))}</span><button className="utility-button" disabled={data.page * data.pageSize >= data.totalCount} onClick={() => page(data.page + 1)}>Trang sau →</button></div>
    </>}
  </PurchasingLayout>;
}
