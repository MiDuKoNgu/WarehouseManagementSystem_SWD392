import { useEffect, useRef, type FormEvent } from 'react';

export type PoTransition = 'confirm' | 'cancel' | 'close';
export const transitionLabels = { confirm: 'Xác nhận PO', cancel: 'Hủy PO', close: 'Đóng PO' };

export function PoTransitionDialog({ action, code, reason, busy, error, onReason, onCancel, onSubmit }: {
  action: PoTransition; code: string; reason: string; busy: boolean; error: string;
  onReason: (value: string) => void; onCancel: () => void; onSubmit: (event: FormEvent) => void;
}) {
  const panel = useRef<HTMLElement>(null);
  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;
    panel.current?.querySelector<HTMLElement>('textarea,button')?.focus();
    return () => previous?.focus();
  }, []);
  return <div className="po-dialog-backdrop"><section className="po-dialog" role="dialog" aria-modal="true" aria-labelledby="po-transition-title" ref={panel} onKeyDown={event => {
    if (event.key === 'Escape' && !busy) onCancel();
    if (event.key !== 'Tab') return;
    const controls = panel.current?.querySelectorAll<HTMLElement>('button:not(:disabled),textarea:not(:disabled)');
    if (!controls?.length) return;
    if (event.shiftKey && document.activeElement === controls[0]) { event.preventDefault(); controls[controls.length - 1].focus(); }
    else if (!event.shiftKey && document.activeElement === controls[controls.length - 1]) { event.preventDefault(); controls[0].focus(); }
  }}>
    <p className="workspace-eyebrow">{code}</p><h2 id="po-transition-title">{transitionLabels[action]}</h2>
    <p>{action === 'confirm' ? 'Sau khi xác nhận, đơn sẽ không thể chỉnh sửa. Nhân viên kho có thể nhận hàng từ PO đã xác nhận.' : action === 'cancel' ? 'Chỉ hủy đơn chưa từng có phiếu nhập. Thao tác này kết thúc đơn mua hàng và được ghi vào Audit Log.' : 'Chỉ đóng đơn đã xác nhận và không còn phiếu nhập mở. Số lượng chưa nhận sẽ được ghi nhận là số lượng đóng thiếu.'}</p>
    <form onSubmit={onSubmit} aria-busy={busy}>
      {action !== 'confirm' && <label className="po-dialog-reason">Lý do {action === 'cancel' ? 'hủy' : 'đóng'}<textarea value={reason} onChange={event => onReason(event.target.value)} required maxLength={1000} rows={4} disabled={busy} /><small>{reason.length}/1000 ký tự</small></label>}
      {error && <p className="po-alert" role="alert">{error}</p>}
      <div className="po-dialog-actions"><button type="button" className="utility-button" onClick={onCancel} disabled={busy}>Quay lại</button><button type="submit" className="po-primary-button" disabled={busy}>{busy ? 'Đang xử lý…' : transitionLabels[action]}</button></div>
    </form>
  </section></div>;
}
