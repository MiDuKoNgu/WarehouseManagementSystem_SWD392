import { useEffect, useId, useRef, useState, type FormEvent } from 'react';
import { SerialCapture } from './serial-capture';
import type { ReceiptDetail, ReceiptItem, SaveReceiptItem } from './types';

type Field = 'line' | 'month' | 'year' | 'condition' | 'note' | 'serial';
type Errors = Partial<Record<Field, string>>;
type Props = {
  data: ReceiptDetail;
  editing: ReceiptItem | null;
  busy: boolean;
  save: (body: SaveReceiptItem) => Promise<string | null>;
  cancel: () => void;
};

export function SerialForm({ data, editing, busy, save, cancel }: Props) {
  const [line, setLine] = useState(String(data.lines[0]?.poLineId || ''));
  const [month, setMonth] = useState('');
  const [year, setYear] = useState('');
  const [condition, setCondition] = useState('GOOD');
  const [note, setNote] = useState('');
  const [serial, setSerial] = useState('');
  const [method, setMethod] = useState('MANUAL');
  const [retain, setRetain] = useState(true);
  const [errors, setErrors] = useState<Errors>({});
  const [saveError, setSaveError] = useState('');
  const [toast, setToast] = useState<{ text: string; kind: 'error' | 'success' } | null>(null);
  const [generation, setGeneration] = useState(0);
  const input = useRef<HTMLInputElement>(null);
  const form = useRef<HTMLFormElement>(null);
  const prefix = useId();

  useEffect(() => {
    if (editing) {
      setLine(String(editing.poLineId));
      setMonth(String(editing.manufacturingMonth || ''));
      setYear(String(editing.manufacturingYear || ''));
      setCondition(editing.receivedCondition || 'GOOD');
      setNote(editing.conditionNote || '');
      setSerial(editing.scannedSerial);
      setMethod(['CAMERA', 'IMAGE_UPLOAD', 'MANUAL'].includes(editing.inputMethod) ? editing.inputMethod : 'MANUAL');
    } else {
      setSerial(''); setMethod('MANUAL'); setCondition('GOOD'); setNote('');
    }
    setErrors({}); setSaveError(''); setToast(previous => !editing && previous?.kind === 'success' ? previous : null); setGeneration(g => g + 1);
  }, [editing]);

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 7000);
    return () => window.clearTimeout(timer);
  }, [toast]);

  function validate(): Errors {
    const result: Errors = {};
    const m = Number(month); const y = Number(year);
    const vn = new Intl.DateTimeFormat('en-CA', { year: 'numeric', month: '2-digit', timeZone: 'Asia/Ho_Chi_Minh' }).formatToParts(new Date());
    const cy = Number(vn.find(p => p.type === 'year')?.value);
    const cm = Number(vn.find(p => p.type === 'month')?.value);
    if (!data.lines.some(l => String(l.poLineId) === line)) result.line = 'Vui lòng chọn sản phẩm.';
    if (!month || !Number.isInteger(m) || m < 1 || m > 12) result.month = 'Vui lòng chọn tháng sản xuất.';
    if (!year || !Number.isInteger(y) || y < 1900 || y > cy) result.year = `Năm sản xuất phải từ 1900 đến ${cy}.`;
    if (!result.month && !result.year && y === cy && m > cm) result.month = 'Tháng/năm sản xuất không được ở tương lai.';
    if (condition === 'REJECTED') result.condition = 'Không tiếp nhận hàng hỏng nặng. Không được thêm Serial này vào phiếu.';
    if (condition === 'DAMAGED' && !note.trim()) result.note = 'Hàng hỏng cần mô tả tình trạng.';
    if (note.length > 1000) result.note = 'Ghi chú tình trạng tối đa 1000 ký tự.';
    if (!serial.trim()) result.serial = 'Vui lòng nhập Serial.';
    else if (serial.trim().length > 100) result.serial = 'Serial tối đa 100 ký tự.';
    return result;
  }

  function clear(...fields: Field[]) {
    setErrors(previous => {
      const next = { ...previous };
      fields.forEach(field => delete next[field]);
      return next;
    });
    setSaveError(''); setToast(null);
  }

  function checkField(field: Field) {
    const text = validate()[field];
    setErrors(previous => ({ ...previous, [field]: text }));
    if (text) setToast({ text, kind: 'error' });
  }

  function fieldProps(field: Field) {
    return {
      'aria-invalid': !!errors[field],
      'aria-describedby': errors[field] ? `${prefix}-${field}-error` : undefined,
      onBlur: () => checkField(field),
    };
  }

  function fieldError(field: Field) {
    return errors[field] ? <small className="inbound-field-error" id={`${prefix}-${field}-error`}>{errors[field]}</small> : null;
  }

  function changeProduct(value: string) {
    setLine(value); setMonth(''); setYear(''); setSerial(''); setMethod('MANUAL'); setNote(''); setCondition('GOOD');
    setErrors({}); setSaveError(''); setToast(null); setGeneration(g => g + 1);
  }

  async function submit(e: FormEvent) {
    e.preventDefault(); if (busy) return;
    setSaveError(''); setToast(null);
    const next = validate(); setErrors(next);
    if (Object.keys(next).length) {
      setToast({ text: Object.values(next).join(' '), kind: 'error' });
      const first = Object.keys(next)[0];
      form.current?.querySelector<HTMLElement>(`[name="${first}"]`)?.focus();
      return;
    }
    const failure = await save({ poLineId: Number(line), serial: serial.trim(), manufacturingMonth: Number(month), manufacturingYear: Number(year), receivedCondition: condition, conditionNote: note, inputMethod: method, rowVersion: data.receipt.rowVersion });
    if (failure) {
      setSaveError(failure); setToast({ text: failure, kind: 'error' });
      return;
    }
    setErrors({}); setSerial(''); setMethod('MANUAL'); setNote(''); setCondition('GOOD'); setGeneration(g => g + 1);
    if (!retain) { setMonth(''); setYear(''); }
    input.current?.focus();
    setToast({ text: editing ? 'Đã cập nhật Serial thành công.' : 'Đã thêm Serial thành công.', kind: 'success' });
  }

  return <section className="po-card inbound-serial-form">
    <h2>{editing ? 'Sửa Serial nháp' : 'Tiếp nhận Serial'}</h2>
    {toast && <div className={`inbound-error-toast${toast.kind === 'success' ? ' inbound-success-toast' : ''}`} role={toast.kind === 'success' ? 'status' : 'alert'} aria-atomic="true">
      <div><strong>{toast.kind === 'success' ? 'Thành công' : 'Chưa thể lưu Serial'}</strong><p>{toast.text}</p></div>
      <button type="button" aria-label={toast.kind === 'success' ? 'Đóng thông báo thành công' : 'Đóng thông báo lỗi'} onClick={() => setToast(null)}>×</button>
    </div>}
    <form ref={form} noValidate onSubmit={e => void submit(e)}>
      <fieldset className="po-fieldset" disabled={busy}>
        <div className="inbound-form-group">
          <h3>1. Thông tin sản phẩm</h3>
          <label>Sản phẩm<select name="line" required value={line} {...fieldProps('line')} onChange={e => changeProduct(e.target.value)}>
            <option value="">Chọn dòng PO</option>
            {data.lines.map(l => <option key={l.poLineId} value={l.poLineId}>{l.productCode} · {l.productName} · còn {l.remainingQuantity}</option>)}
          </select></label>{fieldError('line')}
        </div>
        <div className="inbound-form-group">
          <h3>2. Sản xuất & tình trạng</h3>
          <div className="inbound-two-columns">
            <div><label>Tháng sản xuất<select name="month" required value={month} {...fieldProps('month')} onChange={e => { setMonth(e.target.value); clear('month', 'year'); }}>
              <option value="">Tháng</option>{Array.from({ length: 12 }, (_, i) => <option key={i} value={i + 1}>{i + 1}</option>)}
            </select></label>{fieldError('month')}</div>
            <div><label>Năm sản xuất<input name="year" required type="number" min={1900} step={1} value={year} {...fieldProps('year')} onChange={e => { setYear(e.target.value); clear('month', 'year'); }} inputMode="numeric" /></label>{fieldError('year')}</div>
          </div>
          <label className="inbound-checkbox"><input type="checkbox" checked={retain} onChange={e => setRetain(e.target.checked)} />Giữ tháng/năm cho Serial tiếp theo</label>
          <label>Tình trạng<select name="condition" value={condition} {...fieldProps('condition')} onChange={e => { setCondition(e.target.value); clear('condition', 'note'); }}>
            <option value="GOOD">GOOD — Hàng tốt</option>
            {data.policy.acceptDamaged && <option value="DAMAGED">DAMAGED — Hỏng nhẹ / móp méo — nhận cách ly</option>}
            <option value="REJECTED">Hỏng nặng — không tiếp nhận</option>
          </select></label>{fieldError('condition')}
          <label>Ghi chú tình trạng<textarea name="note" required={condition === 'DAMAGED'} maxLength={1000} value={note} {...fieldProps('note')} onChange={e => { setNote(e.target.value); clear('note'); }} /></label>{fieldError('note')}
          {condition === 'DAMAGED' && <p className="po-helper">Chỉ tiếp nhận hàng hỏng nhẹ / móp méo sau khi kiểm tra. Thiết bị sẽ bị cách ly, không được ghi nhận vào nguồn hàng khả dụng.</p>}
          {condition === 'REJECTED' && <p className="po-alert" role="alert">Không tiếp nhận thiết bị này. Không thêm Serial vào phiếu; tách hàng hỏng nặng để xử lý với nhà cung cấp.</p>}
        </div>
        <div className="inbound-form-group">
          <h3>3. Nhận diện Serial</h3>
          <SerialCapture resetKey={`${line}-${generation}`} disabled={busy || !line} onRescan={() => { setSerial(''); setMethod('MANUAL'); clear('serial'); }} onChoose={(value, source) => { setSerial(value); setMethod(source); clear('serial'); input.current?.focus(); }} />
          <label>Serial<input name="serial" ref={input} required maxLength={100} autoComplete="off" value={serial} {...fieldProps('serial')} onChange={e => { setSerial(e.target.value); setMethod('MANUAL'); clear('serial'); }} /></label>{fieldError('serial')}
          <small className="po-muted">Nguồn: {method === 'CAMERA' ? 'Camera' : method === 'IMAGE_UPLOAD' ? 'Ảnh tem' : 'Nhập tay'} · Enter để thêm nhanh.</small>
        </div>
        {saveError && <div className="po-alert inbound-save-error" role="alert"><p>{saveError}</p></div>}
        <div className="po-actions">
          <button className="po-primary-button" disabled={busy || condition === 'REJECTED'}>{busy ? 'Đang lưu…' : editing ? 'Lưu thay đổi Serial' : 'Xác nhận thêm Serial'}</button>
          {editing && <button className="utility-button" type="button" onClick={cancel}>Dừng sửa</button>}
        </div>
      </fieldset>
    </form>
  </section>;
}
