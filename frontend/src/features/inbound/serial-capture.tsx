import { useEffect, useRef, useState } from 'react';
import type { IScannerControls } from '@zxing/browser';
import { ErrorNotice, message } from './layout';
import { recognizeLabel, scanConsensus, singleImageSerial, type ImageRecognition } from './recognition';

type SerialCaptureProps = {
  onChoose: (serial: string, method: 'CAMERA' | 'IMAGE_UPLOAD') => void;
  onRescan?: () => void; resetKey: string; disabled: boolean;
  closeCameraOnChoose?: boolean; helpText?: string; cameraResultText?: string;
};

export function SerialCapture({ onChoose, onRescan, resetKey, disabled, closeCameraOnChoose = false,
  helpText = 'Quét hoặc đọc ảnh chỉ điền Serial. Kiểm tra kết quả rồi bấm thêm để lưu.',
  cameraResultText = 'Đã tự điền vào ô Serial. Đối chiếu tem trước khi thêm vào phiếu.',
}: SerialCaptureProps) {
  const [camera, setCamera] = useState(false); const [error, setError] = useState(''); const [cameraValue, setCameraValue] = useState('');
  const [result, setResult] = useState<ImageRecognition | null>(null); const [busy, setBusy] = useState(false);
  const controls = useRef<IScannerControls | null>(null); const video = useRef<HTMLVideoElement>(null); const abortImage = useRef<AbortController | null>(null); const consensus = useRef(scanConsensus());
  const latest = useRef({ onChoose, disabled, resetKey, closeCameraOnChoose });
  latest.current = { onChoose, disabled, resetKey, closeCameraOnChoose };
  useEffect(() => { consensus.current.reset(); setCamera(false); setCameraValue(''); setResult(null); abortImage.current?.abort(); setBusy(false); }, [resetKey]);
  useEffect(() => { if (disabled) { setCamera(false); abortImage.current?.abort(); setBusy(false); } }, [disabled]);
  useEffect(() => () => { abortImage.current?.abort(); }, []);
  useEffect(() => {
    if (!camera || disabled) return;
    consensus.current.reset(); setCameraValue('');
    let disposed = false; const sessionKey = resetKey; const preview = video.current;
    async function open() {
      try {
        if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) throw new Error('Camera cần HTTPS (hoặc localhost) và trình duyệt hỗ trợ camera.');
        const { BrowserMultiFormatReader } = await import('@zxing/browser');
        const { barcodeHints } = await import('./barcode-hints');
        if (disposed || !video.current) return;
        // Decode exactly the visible centre guide, rather than unrelated codes elsewhere in the frame.
        class CentreReader extends BrowserMultiFormatReader {
          private crop = document.createElement('canvas');
          decodeFromCanvas(frame: HTMLCanvasElement) {
            this.crop.width = Math.max(1, Math.round(frame.width * .9));
            this.crop.height = Math.max(1, Math.round(frame.height * .6));
            const ctx = this.crop.getContext('2d');
            if (!ctx) return super.decodeFromCanvas(frame);
            ctx.drawImage(frame, frame.width * .05, frame.height * .2, frame.width * .9, frame.height * .6, 0, 0, this.crop.width, this.crop.height);
            return super.decodeFromCanvas(this.crop);
          }
        }
        const scanner = new CentreReader(barcodeHints(true), { delayBetweenScanAttempts: 150, delayBetweenScanSuccess: 150 });
        const handle = await scanner.decodeFromConstraints({ video: { facingMode: { ideal: 'environment' }, width: { ideal: 1920 }, height: { ideal: 1080 } }, audio: false }, video.current, (read, _error, handle) => {
          if (disposed || latest.current.disabled || latest.current.resetKey !== sessionKey) { handle.stop(); return; }
          const value = read && consensus.current.read(read.getText());
          if (value) {
            setCameraValue(value);
            if (latest.current.closeCameraOnChoose) { disposed = true; handle.stop(); controls.current = null; setCamera(false); }
            latest.current.onChoose(value, 'CAMERA');
          }
        });
        if (disposed) handle.stop(); else controls.current = handle;
      } catch (e) {
        if (!disposed) {
          setError(e instanceof DOMException && e.name === 'NotAllowedError' ? 'Bạn chưa cấp quyền camera. Cho phép camera trong trình duyệt hoặc tải ảnh/nhập tay.' : message(e));
          setCamera(false);
        }
      }
    }
    void open();
    return () => { disposed = true; controls.current?.stop(); controls.current = null; if (preview) { const stream = preview.srcObject; if (typeof MediaStream !== 'undefined' && stream instanceof MediaStream) stream.getTracks().forEach(track => track.stop()); preview.srcObject = null; } };
  }, [camera, disabled]);
  function rescan() {
    abortImage.current?.abort(); consensus.current.reset();
    setCameraValue(''); setResult(null); setError(''); setBusy(false);
    onRescan?.();
  }
  async function upload(file: File | undefined) {
    if (!file) return; setCamera(false); abortImage.current?.abort(); const controller = new AbortController(); abortImage.current = controller;
    onRescan?.();
    setBusy(true); setResult(null); setError('');
    try {
      const data = await recognizeLabel(file, controller.signal);
      if (!controller.signal.aborted) {
        setResult(data);
        const serial = singleImageSerial(data);
        if (serial && !latest.current.disabled) latest.current.onChoose(serial, 'IMAGE_UPLOAD');
      }
    }
    catch (e) { if (!controller.signal.aborted) setError(message(e)); }
    finally { if (!controller.signal.aborted) setBusy(false); }
  }
  return <div className="serial-capture"><p className="po-muted">{helpText}</p><ErrorNotice error={error} />
    <div className="po-actions"><button type="button" className="utility-button" disabled={!camera && (disabled || busy)} onClick={() => { setError(''); setCamera(v => !v); }}>{camera ? 'Đóng camera' : 'Quét bằng camera'}</button><label className="utility-button inbound-upload">Tải ảnh tem<input type="file" accept="image/jpeg,image/png,image/webp" disabled={disabled || busy} onChange={e => { void upload(e.target.files?.[0]); e.target.value = ''; }} /></label><button type="button" className="utility-button" disabled={disabled} onClick={rescan}>Quét lại</button></div>
    <small className="po-muted">JPEG, PNG, WebP · tối đa 10 MB · chọn ảnh hoặc chụp ảnh từ điện thoại.</small>
    {camera && <div className="inbound-camera"><div className="inbound-camera-preview"><video ref={video} autoPlay muted playsInline aria-label="Khung quét camera" /><div className="inbound-camera-guide" aria-hidden="true" /></div><p>Đặt duy nhất barcode Serial vào khung giữa, giữ máy yên đến khi đọc trùng 3 lần. Tránh các barcode khác bên cạnh.</p>{cameraValue ? <><p className="inbound-serial">{cameraValue}</p><p>{cameraResultText}</p></> : <p role="status">Đang chờ mã đọc ổn định…</p>}</div>}
    {busy && <div role="status" className="po-helper">Đang đọc barcode và chữ trên tem… <button className="utility-button" type="button" onClick={() => { abortImage.current?.abort(); setBusy(false); }}>Dừng đọc ảnh</button></div>}
    {result && !singleImageSerial(result) && <div className="inbound-recognition"><h4>Kết quả nhận diện — cần chọn Serial</h4>{result.warnings.map(w => <p className="po-inline-error" role="alert" key={w}>{w}</p>)}{[...new Set([...result.barcodes, ...result.ocr])].map(value => <button className="utility-button" type="button" key={value} disabled={disabled} onClick={() => onChoose(value, 'IMAGE_UPLOAD')}><span>{value}</span>Chọn</button>)}{!result.barcodes.length && !result.ocr.length && <p>Chưa đọc được Serial. Thử ảnh rõ hơn hoặc nhập thủ công.</p>}</div>}
  </div>;
}




