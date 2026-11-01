import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { date, ErrorNotice, InboundLayout, message } from './layout';
import { putawayApi, type PutawayCandidate, type PutawayPending, type PutawaySerial, type PutawaySuggestions } from './putaway-api';
import { PutawaySerialCapture } from './putaway-serial-capture';
import { emptyLocationFilters, matchesLocation, type LocationFilters } from './putaway-location-filter';
import { PutawayLocationFilters } from './putaway-location-filters';
import './putaway.css';

const pageSize = 20;
const number = (value: number | null | undefined) => value == null ? '—' : value.toLocaleString('vi-VN', { maximumFractionDigits: 3 });
const condition = (value: string) => value === 'GOOD' ? 'GOOD · Hàng tốt' : value === 'DAMAGED' ? 'DAMAGED · Hàng hỏng' : value;

export function PendingPutawayPage() {
  const [pending, setPending] = useState<PutawayPending | null>(null);
  const [loading, setLoading] = useState(true); const [listError, setListError] = useState('');
  const [search, setSearch] = useState(''); const [query, setQuery] = useState('');
  const [page, setPage] = useState(1); const [attempt, setAttempt] = useState(0);
  const [scan, setScan] = useState(''); const [detail, setDetail] = useState<PutawaySerial | null>(null);
  const [suggestions, setSuggestions] = useState<PutawaySuggestions | null>(null);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [detailLoading, setDetailLoading] = useState(false); const [error, setError] = useState('');
  const [success, setSuccess] = useState(''); const [confirming, setConfirming] = useState(false);
  const [saving, setSaving] = useState(false); const [needsRefresh, setNeedsRefresh] = useState(false);
  const [locationQuery, setLocationQuery] = useState('');
  const [locationFilters, setLocationFilters] = useState<LocationFilters>({ ...emptyLocationFilters });
  const [captureGeneration, setCaptureGeneration] = useState(0);
  const scanInput = useRef<HTMLInputElement>(null); const operation = useRef(0);
  const detailPanel = useRef<HTMLElement>(null);
  const detailRequest = useRef<AbortController | null>(null);

  useEffect(() => {
    const controller = new AbortController(); let active = true;
    setLoading(true); setListError('');
    putawayApi.pending(query, page, pageSize, controller.signal)
      .then(data => {
        if (!active) return;
        if (page > 1 && !data.items.length) { setPage(p => p - 1); return; }
        setPending(data);
      })
      .catch(e => { if (active) setListError(message(e)); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; controller.abort(); };
  }, [query, page, attempt]);
  useEffect(() => () => { operation.current++; detailRequest.current?.abort(); }, []);
  useEffect(() => { if (!saving && detail === null) scanInput.current?.focus(); }, [saving, detail]);
  useEffect(() => {
    if (!detailLoading && (detail || error) && window.innerWidth <= 1200) detailPanel.current?.scrollIntoView?.({ behavior: 'smooth', block: 'start' });
  }, [detailLoading, detail]);

  async function openSerial(serialNumber: string, preserveSelection = false) {
    if (saving || !serialNumber.trim()) return;
    const current = ++operation.current;
    detailRequest.current?.abort();
    const controller = new AbortController(); detailRequest.current = controller;
    setDetailLoading(true); setError(''); setConfirming(false); setSuccess(''); setNeedsRefresh(true);
    if (!preserveSelection) {
      setDetail(null); setSuggestions(null); setSelectedId(null);
      setLocationQuery(''); setLocationFilters({ ...emptyLocationFilters }); setCaptureGeneration(value => value + 1);
    }
    try {
      const serial = await putawayApi.serial(serialNumber.trim(), controller.signal);
      if (current !== operation.current) return;
      setDetail(serial); setScan(serial.serialNumber);
      if (serial.currentLocationId !== null) { setSuggestions(null); setSelectedId(null); setNeedsRefresh(false); return; }
      const data = await putawayApi.suggestions(serial.serialItemId, controller.signal);
      if (current !== operation.current) return;
      setDetail(data.serial); setSuggestions(data); setNeedsRefresh(false);
      const valid = data.candidates.filter(c => c.isValid);
      setSelectedId(previous => preserveSelection && valid.some(c => c.locationId === previous) ? previous : valid[0]?.locationId ?? null);
    } catch (e) { if (current === operation.current) setError(message(e)); }
    finally { if (current === operation.current) setDetailLoading(false); }
  }

  async function confirm() {
    if (!detail || !selectedId || saving || needsRefresh || detailLoading) return;
    setSaving(true); setError(''); setSuccess('');
    try {
      const result = await putawayApi.confirm(detail.serialItemId, selectedId, detail.rowVersion);
      setSuccess(`Đã xếp ${result.serialNumber} tại ${result.locationPath}. ${date(result.occurredAt)} · ${result.actorName || `Nhân viên #${result.actorUserId}`}.`);
      setConfirming(false); setDetail(null); setSuggestions(null); setSelectedId(null); setScan('');
      setPending(previous => previous ? { ...previous, totalCount: Math.max(0, previous.totalCount - 1), items: previous.items.filter(i => i.serialItemId !== result.serialItemId) } : null);
      setAttempt(a => a + 1); scanInput.current?.focus();
    } catch (e) {
      setError(message(e)); setNeedsRefresh(true);
      // A response can be lost after commit; reload authoritative state before another attempt.
    } finally { setSaving(false); }
  }

  const selected = suggestions?.candidates.find(c => c.locationId === selectedId && c.isValid) ?? null;
  const candidates = suggestions?.candidates.filter(c => c.isValid) ?? [];
  const visibleCandidates = candidates.filter(candidate => matchesLocation(candidate, locationQuery, locationFilters));
  const selectionVisible = !!selected && visibleCandidates.some(candidate => candidate.locationId === selected.locationId);
  const unavailable = detail ? unavailableReason(detail) : '';
  const pageCount = Math.max(1, Math.ceil((pending?.totalCount ?? 0) / pageSize));
  function submitScan(event: FormEvent) { event.preventDefault(); void openSerial(scan); }

  return <InboundLayout>
    <Link className="po-back-link" to="/warehouse/inbounds">← Phiếu nhập kho</Link>
    <div className="po-heading"><div><p className="workspace-eyebrow">PUTAWAY / SET LOCATION</p><h1>Thiết bị chờ xếp kho</h1><p>Quét hoặc chọn Serial đã tiếp nhận, chọn vị trí rồi xác nhận xếp kho.</p></div><span className="quiet-badge putaway-count">{pending?.totalCount ?? '—'} thiết bị chờ</span></div>
    {success && <p className="po-success" role="status">{success}</p>}
    <section className="po-card putaway-scan"><h2>Quét Serial</h2><form onSubmit={submitScan}>
      <PutawaySerialCapture resetKey={`lookup-${captureGeneration}`} disabled={saving || detailLoading || confirming} onChoose={value => { setScan(value); scanInput.current?.focus(); }} onRescan={() => setScan('')} />
      <label htmlFor="putaway-scan">Serial cần xếp kho</label><div className="putaway-input-action"><input ref={scanInput} id="putaway-scan" value={scan} maxLength={100} autoComplete="off" placeholder="Quét bằng camera, máy quét hoặc nhập Serial rồi Enter" disabled={saving || detailLoading || confirming} onChange={e => setScan(e.target.value)} /><button className="po-primary-button" disabled={saving || detailLoading || confirming || !scan.trim()}>{detailLoading ? 'Đang tra cứu…' : 'Tra cứu Serial'}</button></div></form><p className="po-muted">Camera, ảnh tem và máy quét dạng bàn phím đều được hỗ trợ. Chỉ thao tác trong kho được phân công.</p></section>
    <div className="putaway-workspace">
      <section className="putaway-pending" aria-label="Danh sách thiết bị chờ">
        <h2>Danh sách chờ</h2>
        <form className="putaway-search" onSubmit={e => { e.preventDefault(); setPage(1); setQuery(search.trim()); }}><label htmlFor="putaway-search">Tìm Serial / sản phẩm / phiếu nhập</label><div className="putaway-input-action"><input id="putaway-search" type="search" value={search} maxLength={100} onChange={e => setSearch(e.target.value)} /><button className="utility-button" disabled={loading}>Tìm</button></div></form>
        <ErrorNotice error={listError} retry={() => setAttempt(a => a + 1)} />
        {loading ? <p role="status">Đang tải thiết bị chờ Putaway…</p> : <>
          <div className="putaway-pending-list">{pending?.items.map(item => <article className={`po-card inbound-item ${detail?.serialItemId === item.serialItemId ? 'inbound-item-editing' : ''}`} key={item.serialItemId}>
            <h3 className="inbound-serial">{item.serialNumber}</h3><p className="putaway-product-name">{item.product?.productName || 'Chưa có thông tin sản phẩm'}<small className="inbound-block po-muted">{item.product?.productCode || `Mã sản phẩm #${item.productId}`}</small></p>
            <dl><div><dt>Loại / Hãng</dt><dd>{item.product?.categoryName || '—'} / {item.product?.brandName || '—'}</dd></div><div><dt>Kho hiện tại</dt><dd>{item.warehouseName}</dd></div><div><dt>Tình trạng</dt><dd><ConditionBadge value={item.itemCondition} /></dd></div><div><dt>Giữ hàng / Cách ly</dt><dd>{claimLabel(item)} · {item.isQuarantined ? 'Đang cách ly' : 'Không cách ly'}</dd></div></dl>
            <p>{item.originalInboundId && item.receiptCode ? <Link className="po-code-link" to={`/warehouse/inbounds/${item.originalInboundId}`}>{item.receiptCode}</Link> : item.receiptCode || item.originType || 'Nguồn nhận chính thức khác'}<small className="inbound-block po-muted">Tiếp nhận: {date(item.receivedAt)}</small></p>
            <button className="utility-button" disabled={saving || detailLoading} onClick={() => void openSerial(item.serialNumber)}>Chọn Serial <span className="sr-only">{item.serialNumber}</span> →</button>
          </article>)}</div>
          {!pending?.items.length && !listError && <div className="po-empty"><h3>Chưa có thiết bị phù hợp</h3><p>{query ? 'Thử tìm Serial, sản phẩm hoặc phiếu nhập khác.' : 'Hoàn tất phiếu nhập để đưa thiết bị vào danh sách chờ xếp kho.'}</p></div>}
          <nav className="putaway-pagination" aria-label="Phân trang thiết bị chờ"><button className="utility-button" disabled={page <= 1 || loading} onClick={() => setPage(p => p - 1)}>← Trước</button><span>Trang {page} / {pageCount}</span><button className="utility-button" disabled={page >= pageCount || loading} onClick={() => setPage(p => p + 1)}>Sau →</button></nav>
        </>}
      </section>
      <section ref={detailPanel} className="putaway-detail" aria-label="Chi tiết xếp kho" aria-busy={detailLoading}>
        <ErrorNotice error={error} />
        {detailLoading && <p role="status">Đang kiểm tra Serial và sức chứa vị trí…</p>}
        {!detail && !detailLoading && <div className="po-card putaway-placeholder"><span aria-hidden="true">▦</span><h2>Chọn thiết bị để xếp kho</h2><p>Tra cứu Serial hoặc chọn thiết bị trong danh sách chờ để xem các vị trí hợp lệ.</p></div>}
        {detail && <>
          <section className="po-card putaway-profile"><p className="workspace-eyebrow">THIẾT BỊ ĐÃ CHỌN</p><h2 className="inbound-serial">{detail.serialNumber}</h2><h3>{detail.product?.productName || 'Chưa có thông tin sản phẩm'}</h3>
            <dl className="po-details"><div><dt>Mã sản phẩm</dt><dd>{detail.product?.productCode || '—'}</dd></div><div><dt>Loại / Hãng</dt><dd>{detail.product?.categoryName || '—'} / {detail.product?.brandName || '—'}</dd></div><div><dt>Kho hiện tại</dt><dd>{detail.warehouseName}</dd></div><div><dt>Tình trạng</dt><dd><ConditionBadge value={detail.itemCondition} /></dd></div><div><dt>Kích thước kiện (dài × rộng × cao)</dt><dd>{number(detail.product?.lengthCm)} × {number(detail.product?.widthCm)} × {number(detail.product?.heightCm)} cm</dd></div><div><dt>Khối lượng</dt><dd>{number(detail.product?.grossWeightKg)} kg</dd></div><div><dt>Trạng thái kho</dt><dd>{detail.stockState}</dd></div><div><dt>Giữ hàng / Khóa kiểm kê</dt><dd>{claimLabel(detail)}<small className="inbound-block">{detail.hasBlockingLock ? 'Đang khóa' : detail.stocktakeChecksAvailable ? 'Không bị khóa kiểm kê' : 'Thông tin khóa kiểm kê chưa khả dụng'}</small></dd></div></dl>
            {detail.isQuarantined && <p className="putaway-quarantine">Đang cách ly. Putaway giữ nguyên cách ly và tình trạng thiết bị; hàng DAMAGED chỉ vào vị trí DAMAGED.</p>}
            {detail.currentLocationId !== null && <p className="po-helper" role="status">Serial đã được đặt tại <strong>{detail.currentLocationPath || `Vị trí #${detail.currentLocationId}`}</strong>. Không thể Putaway lần nữa.</p>}
            {unavailable && <p className="po-alert" role="alert">{unavailable}</p>}
          </section>
          {detail.currentLocationId === null && <section className="putaway-locations"><div className="po-heading"><div><h2>Vị trí có thể chọn</h2><p className="po-muted" role="status">{visibleCandidates.length} / {candidates.length} vị trí hợp lệ.</p></div><button className="utility-button" disabled={saving || detailLoading} onClick={() => void openSerial(detail.serialNumber, true)}>Tải lại gợi ý</button></div>
            <PutawayLocationFilters candidates={candidates} query={locationQuery} filters={locationFilters} onQuery={setLocationQuery} onFilters={setLocationFilters} onReset={() => { setLocationQuery(''); setLocationFilters({ ...emptyLocationFilters }); }} disabled={saving || detailLoading} />
            {suggestions?.reason && <p className="po-alert" role="status">{suggestions.reason}</p>}
            {!candidates.length && !detailLoading && !suggestions?.reason && <p className="po-empty">Chưa có vị trí phù hợp. Kiểm tra kích thước, tải trọng, tình trạng và cấu hình khu lưu trữ.</p>}
            {[false, true].map(reserve => {
              const group = visibleCandidates.filter(c => c.isReserve === reserve);
              return group.length ? <fieldset className="putaway-location-group" key={String(reserve)} disabled={saving || detailLoading || !!unavailable || needsRefresh}><legend>{reserve ? 'Dự trữ RESERVE' : 'Ưu tiên STANDARD'} <span className="quiet-badge">{group.length}</span></legend><div className="putaway-candidate-list">{group.map(candidate => <LocationOption key={candidate.locationId} candidate={candidate} selected={candidate.locationId === selectedId} choose={() => setSelectedId(candidate.locationId)} />)}</div></fieldset> : null;
            })}
            {candidates.length > 0 && !visibleCandidates.length && !detailLoading && <p className="po-empty" role="status">Không có vị trí khớp bộ lọc. Thử đổi Zone, Dãy, Kệ hoặc xóa bộ lọc.</p>}
            <p className="po-muted putaway-packing-note">Ô sàn xếp cạnh nhau, không chồng kiện hoặc tự xoay. Mô phỏng bố trí không lưu tọa độ thực tế; kiểm tra và sắp xếp kiện tại ô theo đúng hướng trước khi xác nhận.</p>
            {selected && !selectionVisible && <p className="po-helper" role="status">Vị trí đã chọn nằm ngoài bộ lọc. Chọn một vị trí trong kết quả để xác nhận.</p>}
            <div className="po-card putaway-selection"><div><small className="po-muted">VỊ TRÍ ĐÃ CHỌN</small><p>{selected?.locationPath || 'Chưa chọn vị trí'}</p></div><button className="po-primary-button" disabled={!selectionVisible || saving || detailLoading || !!unavailable || needsRefresh} onClick={() => setConfirming(true)}>Xác nhận vị trí →</button></div>
            {needsRefresh && !detailLoading && <p className="po-helper">Tải lại gợi ý để kiểm tra trạng thái mới nhất trước khi tiếp tục.</p>}
          </section>}
        </>}
      </section>
    </div>
    {confirming && detail && selected && <PutawayConfirm serial={detail} location={selected} busy={saving} error={error} needsRefresh={needsRefresh} onCancel={() => setConfirming(false)} onRefresh={() => void openSerial(detail.serialNumber, true)} onConfirm={() => void confirm()} />}
  </InboundLayout>;
}

function unavailableReason(serial: PutawaySerial) {
  if (serial.currentLocationId !== null) return '';
  if (serial.stockState !== 'IN_WAREHOUSE') return 'Serial không còn ở trạng thái IN_WAREHOUSE.';
  if (serial.inboundStatus && serial.inboundStatus !== 'DONE') return 'Phiếu nhập của Serial chưa hoàn tất.';
  if (serial.hasActiveClaim) return 'Serial đang được đặt chỗ; không thể xếp kho.';
  if (serial.hasBlockingLock) return 'Serial đang bị khóa thao tác; không thể xếp kho.';
  return '';
}

function ConditionBadge({ value }: { value: string }) { return <span className={`putaway-condition ${value === 'DAMAGED' ? 'putaway-condition-damaged' : ''}`}>{condition(value)}</span>; }
function claimLabel(serial: PutawaySerial) { return serial.hasActiveClaim ? 'Đã đặt chỗ' : serial.claimChecksAvailable ? 'Chưa đặt chỗ' : 'Thông tin giữ hàng chưa khả dụng'; }

function LocationOption({ candidate: c, selected, choose }: { candidate: PutawayCandidate; selected: boolean; choose: () => void }) {
  const floor = c.capacityMode === 'FLOOR_AREA'; const unit = floor ? 'm²' : 'm³';
  const total = floor ? c.totalAreaM2 : c.totalVolumeM3; const used = floor ? c.usedAreaM2 : c.usedVolumeM3;
  const available = floor ? c.availableAreaM2 : c.availableVolumeM3;
  return <label className={`po-card putaway-location-option ${selected ? 'putaway-location-selected' : ''}`}><div className="putaway-location-title"><input type="radio" name="putaway-location" value={c.locationId} checked={selected} onChange={choose} /><span>{c.locationPath}</span><span className="quiet-badge">#{c.suggestionRank}</span></div><div className="putaway-location-body"><small className="po-muted">{c.locationType === 'FLOOR_SLOT' ? 'Ô sàn' : 'Tầng kệ'} · {number(c.lengthCm)} × {number(c.widthCm)} × {number(c.heightCm)} cm · {c.occupantCount} Serial đang đặt</small><dl><div><dt>{floor ? 'Diện tích đã dùng / Tổng' : 'Thể tích đã dùng / Tổng'}</dt><dd>{number(used)} / {number(total)} {unit}</dd></div><div><dt>{floor ? 'Diện tích còn lại' : 'Thể tích còn lại'}</dt><dd>{number(available)} {unit}</dd></div><div><dt>Tải trọng đã dùng / Tối đa</dt><dd>{number(c.usedWeightKg)} / {c.maxWeightKg == null ? 'Chưa cấu hình' : `${number(c.maxWeightKg)} kg`}</dd></div><div><dt>Tải trọng còn lại</dt><dd>{c.remainingWeightKg == null ? 'Chưa cấu hình giới hạn' : `${number(c.remainingWeightKg)} kg`}</dd></div></dl>{c.reason && <small className="po-muted">{c.reason}</small>}</div></label>;
}

function PutawayConfirm({ serial, location, busy, error, needsRefresh, onCancel, onRefresh, onConfirm }: {
  serial: PutawaySerial; location: PutawayCandidate; busy: boolean; error: string; needsRefresh: boolean;
  onCancel: () => void; onRefresh: () => void; onConfirm: () => void;
}) {
  const panel = useRef<HTMLDivElement>(null); const cancelButton = useRef<HTMLButtonElement>(null);
  useEffect(() => { const previous = document.activeElement as HTMLElement | null; cancelButton.current?.focus(); return () => previous?.focus(); }, []);
  function submit(event: FormEvent) {
    event.preventDefault(); if (busy || needsRefresh) return;
    onConfirm();
  }
  return <div className="inbound-modal-backdrop"><div ref={panel} className="po-card inbound-modal putaway-modal" role="dialog" aria-modal="true" aria-labelledby="putaway-confirm-title" tabIndex={-1} onKeyDown={e => {
    if (e.key === 'Escape' && !busy) onCancel();
    if (e.key === 'Tab') {
      const nodes = panel.current?.querySelectorAll<HTMLElement>('button:not(:disabled),input:not(:disabled)');
      if (nodes?.length) { const first = nodes[0]; const last = nodes[nodes.length - 1]; if (e.shiftKey && (document.activeElement === first || document.activeElement === panel.current)) { e.preventDefault(); last.focus(); } else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); } }
    }
  }}><h2 id="putaway-confirm-title">Xác nhận xếp kho</h2><p className="inbound-serial"><strong>{serial.serialNumber}</strong></p><p>{serial.product?.productCode} · {serial.product?.productName}</p><p><strong>Kho:</strong> {serial.warehouseName}<br /><strong>Vị trí:</strong> {location.locationPath}<br /><ConditionBadge value={serial.itemCondition} /></p>{serial.isQuarantined && <p className="putaway-quarantine">Thiết bị tiếp tục được cách ly sau khi xếp kho.</p>}<form onSubmit={submit}><ErrorNotice error={error} />{needsRefresh && <p className="po-helper">Kiểm tra lại trạng thái trước khi gửi lần nữa.</p>}<div className="po-actions"><button ref={cancelButton} className="utility-button" type="button" disabled={busy} onClick={onCancel}>Quay lại</button>{needsRefresh ? <button className="po-primary-button" type="button" onClick={onRefresh}>Tải lại gợi ý</button> : <button className="po-primary-button" disabled={busy}>{busy ? 'Đang xếp kho…' : 'Xác nhận Putaway'}</button>}</div></form></div></div>;
}
