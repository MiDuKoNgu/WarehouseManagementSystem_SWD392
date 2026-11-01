import { useEffect, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { auditApi } from '../api/audit-api';
import { AdminLayout } from '../components/admin-layout';
import { AuditError, AuditStatus, useAuditError } from '../components/audit-common';
import { AuditValues } from '../components/audit-values';
import { actionLabels, formatAuditTime, groupLabels, type AuditDetail } from '../types/audit';

export function AuditDetailPage({ source, id }: { source: string; id: number }) {
  const [detail, setDetail] = useState<AuditDetail | null>(null);
  const [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);
  const explain = useAuditError();
  const location = useLocation();
  const returnTo = typeof location.state?.returnTo === 'string' && location.state.returnTo.startsWith('/admin/audit-logs?') ? location.state.returnTo : '/admin/audit-logs';
  useEffect(() => {
    let cancelled = false;
    setDetail(null); setError('');
    auditApi.detail(source, id).then(data => { if (!cancelled) setDetail(data); }).catch(reason => { if (!cancelled) setError(explain(reason)); });
    return () => { cancelled = true; };
  }, [source, id, attempt, explain]);
  const item = detail?.item;
  return <AdminLayout>
    <Link className="audit-back" to={returnTo} state={location.state?.returnState}>← Lịch sử thao tác</Link>
    {error ? <AuditError message={error} retry={() => setAttempt(value => value + 1)} /> : !detail || !item ? <div className="audit-card audit-loading" role="status">Đang tải chi tiết lịch sử…</div> : <>
      <div className="audit-heading"><div><p className="workspace-eyebrow">CHI TIẾT AUDIT LOG · #{item.auditLogId}</p><h1>{actionLabels[item.action] || item.action}</h1><p>{item.documentCode || `${item.entityName} #${item.entityId}`}</p></div><AuditStatus value={item.status} /></div>
      <section className="audit-card audit-metadata" aria-labelledby="audit-info-title"><h2 id="audit-info-title">Thông tin thao tác</h2><dl>
        <div><dt>Người thực hiện</dt><dd>{item.actorName || `Nhân viên #${item.userId}`} <small>· ID {item.userId}</small></dd></div>
        <div><dt>Thời điểm (giờ Việt Nam)</dt><dd>{formatAuditTime(item.createdAt)}</dd></div>
        <div><dt>Nhóm nghiệp vụ</dt><dd>{groupLabels[item.businessGroup] || item.businessGroup}</dd></div>
        <div><dt>Mã hành động</dt><dd><code>{item.action}</code></dd></div>
        <div><dt>Dữ liệu được thao tác</dt><dd>{item.entityName} · ID {item.entityId}</dd></div>
        <div><dt>Nguồn lịch sử</dt><dd>{item.source === 'procurement' ? 'Dịch vụ mua hàng' : 'Dịch vụ vận hành kho'}</dd></div>
      </dl></section>
      <AuditValues oldValues={detail.oldValues} newValues={detail.newValues} />
      <p className="audit-footnote">Thông tin nhạy cảm được ẩn tại máy chủ. Bản ghi lịch sử chỉ được xem, không thể sửa hoặc xóa.</p>
    </>}
  </AdminLayout>;
}
