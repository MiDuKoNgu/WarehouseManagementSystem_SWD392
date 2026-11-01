import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { SerialCapture } from '../src/features/inbound/serial-capture';
import { serialCandidates, scanConsensus, singleImageSerial, validateLabelImage, recognizeLabel } from '../src/features/inbound/recognition';

const state = vi.hoisted(() => ({ callback: null as null | ((result: { getText: () => string }, error: unknown, controls: { stop: () => void }) => void), stop: vi.fn(), camera: vi.fn(), barcode: 'BARCODE-ABC', ocr: 'Serial: OCR-XYZ', terminate: vi.fn(), worker: vi.fn() }));
vi.mock('@zxing/browser', () => ({ BrowserMultiFormatReader: class {
  async decodeFromConstraints(_constraints: unknown, _video: unknown, callback: typeof state.callback) { state.callback = callback; await state.camera(); return { stop: state.stop }; }
  async decodeFromImageElement() { return { getText: () => state.barcode }; }
  decodeFromCanvas() { throw new Error('no barcode in crop'); }
} }));
vi.mock('tesseract.js', () => ({ PSM: { SPARSE_TEXT: '11', SINGLE_BLOCK: '6' }, createWorker: async () => { await state.worker(); return { setParameters: vi.fn(), recognize: async () => ({ data: { text: state.ocr } }), terminate: state.terminate }; } }));
beforeEach(() => {
  state.callback = null; state.camera.mockReset(); state.worker.mockReset(); state.stop.mockReset(); state.terminate.mockReset(); state.barcode = 'BARCODE-ABC'; state.ocr = 'Serial: OCR-XYZ';
  vi.stubGlobal('isSecureContext', true); vi.stubGlobal('MediaStream', class {});
  Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: { getUserMedia: vi.fn() } });
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.restoreAllMocks(); });

it('reads only labelled OCR Serial candidates while preserving manufacturer case', () => {
  expect(serialCandidates('MODEL: TV-123\nEAN: 123456789\nS/N: AbC-001\nSerial Number: bCd-002\nSKU: TV-ABC')).toEqual(['AbC-001', 'bCd-002']);
});
it('reads the next OCR line after a label with pound-sign noise, never the pound sign', () => {
  expect(serialCandidates('Serial £\n2691A27533437L0 | -\nJRA')).toEqual(['2691A27533437L0']);
  expect(serialCandidates('Serial\n8934804025742')).toEqual(['8934804025742']);
  expect(serialCandidates('Serial £\nSKU: 8934804025742\nMODEL: TV-12')).toEqual([]);
  expect(serialCandidates('Serial £')).toEqual([]);
});
it('does not extract an accidental SN substring inside an unrelated word', () => {
  expect(serialCandidates('DESIGN: ABC123\nMODEL: ABC\nEAN: 8934804025742')).toEqual([]);
});
it('requires three consecutive matching reads, expires stale reads and latches until reset', () => {
  const gate = scanConsensus();
  expect(gate.read('668152', 100)).toBeNull();
  expect(gate.read('8934804025742', 200)).toBeNull();
  expect(gate.read('8934804025742', 300)).toBeNull();
  expect(gate.read('8934804025742', 400)).toBe('8934804025742');
  expect(gate.read('OTHER', 500)).toBeNull();
  gate.reset();
  expect(gate.read('ABC', 100)).toBeNull();
  expect(gate.read('ABC', 2000)).toBeNull();
  expect(gate.read('ABC', 2100)).toBeNull();
  expect(gate.read('ABC', 2200)).toBe('ABC');
});

it('automatically fills a stable camera read once, supports rescan and stops on close', async () => {
  const choose = vi.fn(); const view = render(<SerialCapture resetKey="1" disabled={false} onChoose={choose} />);
  fireEvent.click(screen.getByRole('button', { name: 'Quét bằng camera' })); await waitFor(() => expect(state.callback).not.toBeNull());
  state.callback!({ getText: () => 'AbC-001' }, null, { stop: state.stop }); state.callback!({ getText: () => 'AbC-001' }, null, { stop: state.stop });
  expect(screen.queryByRole('button', { name: 'Dùng mã camera này' })).toBeNull();
  state.callback!({ getText: () => 'AbC-001' }, null, { stop: state.stop });
  await screen.findByText('AbC-001'); expect(choose).toHaveBeenCalledOnce(); expect(choose).toHaveBeenCalledWith('AbC-001', 'CAMERA');
  state.callback!({ getText: () => 'AbC-001' }, null, { stop: state.stop });
  expect(choose).toHaveBeenCalledOnce();
  expect(screen.queryByRole('button', { name: 'Dùng mã camera này' })).toBeNull();
  expect(screen.getByRole('button', { name: 'Đóng camera' })).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Quét lại' }));
  expect(screen.queryByRole('button', { name: 'Dùng mã camera này' })).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Đóng camera' })); expect(state.stop).toHaveBeenCalled();
  view.unmount();
});

it('stops camera even when component unmounts before permission resolves', async () => {
  let release!: () => void; state.camera.mockImplementation(() => new Promise<void>(resolve => { release = resolve; }));
  const view = render(<SerialCapture resetKey="1" disabled={false} onChoose={vi.fn()} />);
  fireEvent.click(screen.getByRole('button', { name: 'Quét bằng camera' })); await waitFor(() => expect(state.callback).not.toBeNull()); view.unmount(); release(); await waitFor(() => expect(state.stop).toHaveBeenCalled());
});

it('explains denied camera permission and insecure context', async () => {
  state.camera.mockRejectedValue(new DOMException('denied', 'NotAllowedError'));
  render(<SerialCapture resetKey="1" disabled={false} onChoose={vi.fn()} />); fireEvent.click(screen.getByRole('button', { name: 'Quét bằng camera' }));
  await screen.findByText('Bạn chưa cấp quyền camera. Cho phép camera trong trình duyệt hoặc tải ảnh/nhập tay.');
  vi.stubGlobal('isSecureContext', false); fireEvent.click(screen.getByRole('button', { name: 'Quét bằng camera' }));
  await screen.findByText('Camera cần HTTPS (hoặc localhost) và trình duyệt hỗ trợ camera.');
});

const png = () => ({ size: 12, type: 'image/png', slice: () => ({ arrayBuffer: async () => new Uint8Array([137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0]).buffer }) }) as File;
it('rejects excessive, unsupported and disguised uploads before decoding', async () => {
  await expect(validateLabelImage({ size: 11 * 1024 * 1024, type: 'image/png' } as File)).rejects.toThrow('10 MB');
  await expect(validateLabelImage({ size: 100, type: 'text/plain' } as File)).rejects.toThrow('JPEG');
  await expect(validateLabelImage({ ...png(), slice: () => ({ arrayBuffer: async () => new ArrayBuffer(12) }) } as unknown as File)).rejects.toThrow('không khớp');
});

function imageEnvironment() {
  vi.stubGlobal('Image', class { src = ''; naturalWidth = 100; naturalHeight = 100; decode = async () => undefined; });
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(null);
  URL.createObjectURL = vi.fn(() => 'blob:image'); URL.revokeObjectURL = vi.fn();
}
it('only automatically selects an unambiguous image value', () => {
  const base = { text: '', warnings: [] };
  expect(singleImageSerial({ ...base, barcodes: ['ABC'], ocr: ['ABC'] })).toBe('ABC');
  expect(singleImageSerial({ ...base, barcodes: ['ABC'], ocr: [] })).toBe('ABC');
  expect(singleImageSerial({ ...base, barcodes: [], ocr: ['ABC'] })).toBe('ABC');
  expect(singleImageSerial({ ...base, barcodes: ['ABC'], ocr: ['XYZ'] })).toBeNull();
  expect(singleImageSerial({ ...base, barcodes: ['ABC', 'XYZ'], ocr: [] })).toBeNull();
});
it('automatically fills a single image result without a choose button', async () => {
  imageEnvironment(); state.barcode = '8934804025742'; state.ocr = 'Serial: 8934804025742';
  const choose = vi.fn(); const { container } = render(<SerialCapture resetKey="1" disabled={false} onChoose={choose} />);
  fireEvent.change(container.querySelector('input[type=file]')!, { target: { files: [png()] } });
  await waitFor(() => expect(choose).toHaveBeenCalledWith('8934804025742', 'IMAGE_UPLOAD'));
  expect(screen.queryByRole('heading', { name: 'Đã tự điền Serial từ ảnh' })).toBeNull();
  expect(screen.queryByText('Chữ OCR nhận diện')).toBeNull();
  expect(screen.getByRole('button', { name: 'Quét lại' })).toBeTruthy();
  expect(screen.queryByRole('button', { name: /Chọn/ })).toBeNull();
});
it('leaves conflicting image candidates for explicit selection', async () => {
  imageEnvironment();
  const choose = vi.fn(); const { container } = render(<SerialCapture resetKey="1" disabled={false} onChoose={choose} />);
  fireEvent.change(container.querySelector('input[type=file]')!, { target: { files: [png()] } });
  await screen.findByRole('heading', { name: 'Kết quả nhận diện — cần chọn Serial' });
  expect(choose).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: /BARCODE-ABC.*Chọn/ }));
  expect(choose).toHaveBeenCalledWith('BARCODE-ABC', 'IMAGE_UPLOAD');
});
it('returns barcode and labelled OCR candidates, warns on disagreement and releases resources', async () => {
  imageEnvironment(); const result = await recognizeLabel(png());
  expect(result.barcodes).toEqual(['BARCODE-ABC']); expect(result.ocr).toEqual(['OCR-XYZ']); expect(result.warnings[0]).toContain('không khớp');
  expect(state.terminate).toHaveBeenCalledOnce(); expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:image');
});
it('keeps barcode candidates when OCR initialization is unavailable', async () => {
  imageEnvironment(); state.worker.mockRejectedValue(new Error('offline')); const result = await recognizeLabel(png());
  expect(result.barcodes).toEqual(['BARCODE-ABC']); expect(result.warnings[0]).toContain('OCR chưa xử lý');
});
it('returns the numeric serial from the user OCR regression rather than an invalid noise candidate', async () => {
  imageEnvironment(); state.ocr = 'Serial £\n8934804025742 | -\nJRA';
  const result = await recognizeLabel(png());
  expect(result.ocr).toEqual(['8934804025742']);
  expect(result.ocr).not.toContain('£');
});
