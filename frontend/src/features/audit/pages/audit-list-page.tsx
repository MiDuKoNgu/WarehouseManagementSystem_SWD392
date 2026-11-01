import { useEffect, useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { auditApi } from '../api/audit-api';
import { AdminLayout } from '../components/admin-layout';
import { AuditError, AuditStatus, useAuditError } from '../components/audit-common';
import { actionLabels, auditQuery, emptyFilters, formatAuditTime, groupLabels, type AuditFilters, type AuditOptions, type AuditPage } from '../types/audit';

function readFilters(search: string): AuditFilters {
  const query = new URLSearchParams(search);
  return Object.fromEntries(Object.keys(emptyFilters).map(key => [key, query.get(key) || ''])) as unknown as AuditFilters;
}

export function AuditListPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const [draft, setDraft] = useState(() => readFilters(location.search));
  const [options, setOptions] = useState<AuditOptions>({ employees: [], actions: [] });
  const [optionsError, setOptionsError] = useState('');
  const [page, setPage] = useState<AuditPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);
  const [validation, setValidation] = useState('');
  const explain = useAuditError();
  const params = new URLSearchParams(location.search);
  const requestedPage = Number(params.get('page') || 1);
  const index = Number.isSafeInteger(requestedPage) && requestedPage > 0 ? requestedPage - 1 : 0;
  const cursorStack: string[] = Array.isArray(location.state?.auditCursors) ? location.state.auditCursors : [''];

  useEffect(() => { setDraft(readFilters(location.search)); }, [location.search]);
  useEffect(() => {
    let cancelled = false;
    setOptionsError('');
    auditApi.options().then(data => { if (!cancelled) setOptions(data); }).catch(reason => { if (!cancelled) setOptionsError(explain(reason)); });
    return () => { cancelled = true; };
  }, [attempt, explain]);
  useEffect(() => {
    let cancelled = false;
    setLoading(true); setError('');
    const filters = readFilters(location.search);
    let query: URLSearchParams;
    try { query = auditQuery(filters, new URLSearchParams(location.search).get('cursor') || ''); }
    catch { setError('Khoảng thời gian chưa hợp lệ. Hãy chọn lại ngày lọc.'); setLoading(false); return; }
    auditApi.list(query).then(data => { if (!cancelled) setPage(data); }).catch(reason => { if (!cancelled) setError(explain(reason)); }).finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [location.search, attempt, explain]);

  function apply(event: FormEvent) {
    event.preventDefault();
    if (draft.from && draft.to && draft.from > draft.to) { setValidation('Ngày bắt đầu không được sau ngày kết thúc.'); return; }
    setValidation('');
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(draft)) if (value.trim()) query.set(key, value.trim());
    navigate({ pathname: '/admin/audit-logs', search: query.toString() }, { state: { auditCursors: [''] } });
  }
  function turn(next: boolean) {
    if (loading || !page) return;
    const nextIndex = next ? index + 1 : index - 1;
    const nextCursor = next ? page.nextCursor : cursorStack[nextIndex];
    if (nextIndex < 0 || nextCursor === undefined || next && !nextCursor) return;
    const stack = next ? [...cursorStack.slice(0, index + 1), nextCursor] : cursorStack;
    const query = new URLSearchParams(location.search);
    if (nextCursor) query.set('cursor', nextCursor); else query.delete('cursor');
    query.set('page', String(nextIndex + 1));
    navigate({ search: query.toString() }, { state: { auditCursors: stack } });
  }
  const change = (key: keyof AuditFilters, value: string) => setDraft(current => ({ ...current, [key]: value }));
  return <AdminLayout>
    <div className="audit-heading"><div><p className="workspace-eyebrow">LỊCH SỬ HỆ THỐNG</p><h1>Mỗi thao tác. Một dấu vết.</h1><p>Đối chiếu ai đã làm gì, vào lúc nào và trên dữ liệu nào.</p></div><span className="audit-sticker" aria-hidden="true">≡</span></div>
    <div className="audit-readonly"><span aria-hidden="true">◈</span><p>Lịch sử chỉ đọc. Audit Log ghi hành động của người dùng; lịch sử di chuyển hàng hóa được quản lý riêng.</p></div>
    <form className="audit-card audit-filters" onSubmit={apply}>
      <div className="audit-filter-grid">
        <label>Nhân viên<select value={draft.userId} onChange={event => change('userId', event.target.value)}><option value="">Tất cả nhân viên</option>{options.employees.map(employee => <option key={employee.userId} value={employee.userId}>{employee.fullName} — {employee.username}</option>)}</select></label>
        <label>Nhóm nghiệp vụ<select value={draft.businessGroup} onChange={event => change('businessGroup', event.target.value)}><option value="">Tất cả nghiệp vụ</option>{Object.entries(groupLabels).map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select></label>
        <label>Hành động<select value={draft.action} onChange={event => change('action', event.target.value)}><option value="">Tất cả hành động</option>{options.actions.map(action => <option key={action} value={action}>{actionLabels[action] || action}</option>)}</select></label>
        <label>Mã chứng từ<input value={draft.documentCode} maxLength={100} onChange={event => change('documentCode', event.target.value)} placeholder="Tìm mã PO, phiếu nhập…" /></label>
        <label>Từ ngày<input type="date" value={draft.from} onChange={event => change('from', event.target.value)} /></label>
        <label>Đến ngày<input type="date" value={draft.to} onChange={event => change('to', event.target.value)} /></label>
      </div>
      <div className="audit-filter-footer"><span>Thời gian theo giờ Việt Nam (UTC+7).</span><div><button type="button" className="utility-button" onClick={() => { setDraft(emptyFilters); setValidation(''); navigate('/admin/audit-logs', { state: { auditCursors: [''] } }); }}>Đặt lại</button><button className="audit-primary" type="submit">Áp dụng bộ lọc →</button></div></div>
      {validation && <p className="audit-error" role="alert">{validation}</p>}
    </form>
    {optionsError && <AuditError message={`Chưa tải được danh sách bộ lọc: ${optionsError}`} retry={() => setAttempt(value => value + 1)} />}
    <div className="audit-result-heading"><h2>Lịch sử thao tác</h2><span>{page && !loading && !error ? `${page.totalCount.toLocaleString('vi-VN')} bản ghi` : 'Mới nhất trước'}</span></div>
    {error ? <AuditError message={error} retry={() => setAttempt(value => value + 1)} /> : loading ? <div className="audit-card audit-loading" role="status">Đang tải lịch sử thao tác…</div> : page && <>
      <div className="audit-card audit-table-wrap"><table className="audit-table"><thead><tr><th>Thời điểm</th><th>Người thực hiện</th><th>Nghiệp vụ / Hành động</th><th>Chứng từ</th><th>Kết quả</th><th><span className="sr-only">Chi tiết</span></th></tr></thead><tbody>{page.items.map(item => <tr key={`${item.source}-${item.auditLogId}`}><td className="audit-time">{formatAuditTime(item.createdAt)}</td><td><strong>{item.actorName || `Nhân viên #${item.userId}`}</strong><small>ID {item.userId}</small></td><td><strong>{actionLabels[item.action] || item.action}</strong><small>{groupLabels[item.businessGroup] || item.businessGroup}</small></td><td><strong>{item.documentCode || item.entityId}</strong><small>{item.entityName} · #{item.entityId}</small></td><td><AuditStatus value={item.status} /></td><td><Link className="audit-detail-link" to={`/admin/audit-logs/${item.source}/${item.auditLogId}`} state={{ returnTo: location.pathname + location.search, returnState: location.state }}>Xem chi tiết →</Link></td></tr>)}</tbody></table>{page.items.length === 0 && <div className="audit-empty"><span aria-hidden="true">≡</span><h3>Chưa có lịch sử phù hợp.</h3><p>Thử thay đổi bộ lọc. Các nghiệp vụ chưa được thực hiện sẽ chưa có bản ghi.</p></div>}</div>
      <div className="audit-pagination"><span>Trang {Number.isFinite(index) ? index + 1 : 1} · Tối đa {page.pageSize} bản ghi/trang</span><div><button className="utility-button" onClick={() => turn(false)} disabled={index === 0 || cursorStack[index - 1] === undefined}>← Trước</button><button className="utility-button" onClick={() => turn(true)} disabled={!page.hasMore || !page.nextCursor}>Sau →</button></div></div>
    </>}
    <p className="audit-footnote">Lịch sử được tổng hợp từ dịch vụ mua hàng và vận hành kho. Không thể sửa hoặc xóa bản ghi trên giao diện.</p>
  </AdminLayout>;
}
