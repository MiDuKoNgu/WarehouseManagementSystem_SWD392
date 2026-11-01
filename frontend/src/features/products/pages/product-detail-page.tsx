import { useEffect, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { PurchasingLayout } from '../../procurement/components/purchasing-layout';
import { formatDate, PoError } from '../../procurement/components/po-common';
import { usePoError } from '../../procurement/hooks/use-po-error';
import { productApi } from '../api/product-api';
import { ProductStatus } from '../components/product-status';
import type { ProductDetail } from '../types/product';

export function ProductDetailPage({ id }: { id: number }) {
  const [data, setData] = useState<ProductDetail | null>(null); const [error, setError] = useState(''); const [attempt, setAttempt] = useState(0);
  const explain = usePoError(); const location = useLocation();
  useEffect(() => { let cancelled = false; setError(''); setData(null); productApi.detail(id).then(value => { if (!cancelled) setData(value); }).catch(reason => { if (!cancelled) setError(explain(reason)); }); return () => { cancelled = true; }; }, [id, attempt, explain]);
  return <PurchasingLayout current="products"><Link className="po-back-link" to="/purchasing/products">← Sản phẩm</Link>
    {error ? <PoError message={error} retry={() => setAttempt(value => value + 1)} /> : !data ? <div className="po-card po-loading" role="status">Đang tải chi tiết sản phẩm…</div> : <>
      {new URLSearchParams(location.search).has('saved') && <div className="po-success" role="status">✓ Đã lưu thông tin sản phẩm và nhà cung cấp.</div>}
      <div className="po-heading"><div><p className="workspace-eyebrow">{data.productCode}</p><h1>{data.productName}</h1><p>{data.categoryName} · {data.brandName}</p></div><Link className="po-primary-button" to={`/purchasing/products/${id}/edit`}>Chỉnh sửa sản phẩm</Link></div>
      <section className="po-card po-form-section"><div className="section-heading"><h2>Thông tin sản phẩm</h2><ProductStatus status={data.status} /></div><dl className="po-details"><div><dt>Mã sản phẩm</dt><dd>{data.productCode}</dd></div><div><dt>Model</dt><dd>{data.model}</dd></div><div><dt>Danh mục</dt><dd>{data.categoryName}</dd></div><div><dt>Hãng</dt><dd>{data.brandName}</dd></div><div><dt>Đơn vị</dt><dd>{data.unit}</dd></div><div><dt>Nhà cung cấp</dt><dd>{data.suppliers.map(item => item.supplierName).join(', ') || 'Chưa có'}</dd></div><div><dt>Ngày tạo</dt><dd>{formatDate(data.createdAt)}</dd></div><div><dt>Cập nhật gần nhất</dt><dd>{formatDate(data.updatedAt)}</dd></div><div><dt>Người tạo</dt><dd>Tài khoản #{data.createdByUserId}</dd></div></dl></section>
      <section className="po-card po-form-section"><h2>Kích thước & khối lượng</h2><dl className="product-dimensions"><div><dt>Dài</dt><dd>{data.lengthCm.toLocaleString('vi-VN')} cm</dd></div><div><dt>Rộng</dt><dd>{data.widthCm.toLocaleString('vi-VN')} cm</dd></div><div><dt>Cao</dt><dd>{data.heightCm.toLocaleString('vi-VN')} cm</dd></div><div><dt>Khối lượng</dt><dd>{data.grossWeightKg.toLocaleString('vi-VN')} kg</dd></div></dl></section>
      <section className="po-card po-form-section"><h2>Thông số kỹ thuật</h2><p className="po-note product-specifications">{data.specifications || 'Chưa có thông số kỹ thuật.'}</p></section>
    </>}
  </PurchasingLayout>;
}
