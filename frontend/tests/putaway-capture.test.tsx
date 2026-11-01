import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { PutawaySerialCapture } from '../src/features/inbound/putaway-serial-capture';

const state = vi.hoisted(() => ({
  callback: null as null | ((result: { getText: () => string } | null, error: unknown, controls: { stop: () => void }) => void),
  stop: vi.fn(), camera: vi.fn(), constraints: null as unknown, video: null as unknown,
}));
vi.mock('@zxing/browser', () => ({ BrowserMultiFormatReader: class {
  async decodeFromConstraints(constraints: unknown, video: unknown, callback: typeof state.callback) {
    state.constraints = constraints; state.video = video; state.callback = callback;
    await state.camera(); return { stop: state.stop };
  }
  decodeFromCanvas() { throw new Error('No barcode in this frame'); }
} }));

beforeEach(() => {
  state.callback = null; state.constraints = null; state.video = null;
  state.stop.mockReset(); state.camera.mockReset();
  vi.stubGlobal('isSecureContext', true); vi.stubGlobal('MediaStream', class {});
  Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: { getUserMedia: vi.fn() } });
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.restoreAllMocks(); });

async function openCamera() {
  fireEvent.click(screen.getByRole('button', { name: 'Quét bằng camera' }));
  await waitFor(() => expect(state.callback).not.toBeNull());
}
function read(value: string, count = 1) {
  act(() => { for (let i = 0; i < count; i++) state.callback!({ getText: () => value }, null, { stop: state.stop }); });
}

it('uses the shared ZXing video decoder, fills after three stable reads and closes without submitting the form', async () => {
  const choose = vi.fn(); const submit = vi.fn((event: React.FormEvent) => event.preventDefault());
  render(<form onSubmit={submit}><PutawaySerialCapture resetKey="lookup-1" disabled={false} onChoose={choose} /></form>);
  expect(screen.getByText('Quét hoặc đọc ảnh chỉ điền Serial. Kiểm tra kết quả trước khi tra cứu hoặc xác nhận xếp kho.')).toBeTruthy();
  expect(screen.queryByText(/trước khi thêm vào phiếu/)).toBeNull();
  await openCamera();
  expect(state.video).toBe(screen.getByLabelText('Khung quét camera'));
  expect(state.constraints).toEqual({ video: { facingMode: { ideal: 'environment' }, width: { ideal: 1920 }, height: { ideal: 1080 } }, audio: false });
  read('SN-AbC/99', 2); expect(choose).not.toHaveBeenCalled(); read('SN-AbC/99');
  await waitFor(() => expect(choose).toHaveBeenCalledExactlyOnceWith('SN-AbC/99'));
  expect(state.stop).toHaveBeenCalled(); expect(screen.queryByLabelText('Khung quét camera')).toBeNull();
  expect(screen.getByRole('button', { name: 'Quét bằng camera' })).toBeTruthy();
  read('OTHER-SERIAL', 3); expect(choose).toHaveBeenCalledOnce(); expect(submit).not.toHaveBeenCalled();
});

it('closes the stream manually and releases it when the capture component unmounts', async () => {
  const choose = vi.fn(); const view = render(<PutawaySerialCapture resetKey="1" disabled={false} onChoose={choose} />);
  await openCamera(); fireEvent.click(screen.getByRole('button', { name: 'Đóng camera' }));
  expect(state.stop).toHaveBeenCalled(); expect(screen.queryByLabelText('Khung quét camera')).toBeNull();
  state.callback = null; state.stop.mockClear(); await openCamera();
  await waitFor(() => expect(state.video).toBeTruthy()); view.unmount();
  await waitFor(() => expect(state.stop).toHaveBeenCalled()); read('LATE-READ', 3); expect(choose).not.toHaveBeenCalled();
});

it('stops a camera request resolving after lookup is disabled and ignores late decoded serials', async () => {
  let release!: () => void; state.camera.mockImplementation(() => new Promise<void>(resolve => { release = resolve; }));
  const choose = vi.fn(); const view = render(<PutawaySerialCapture resetKey="1" disabled={false} onChoose={choose} />);
  await openCamera(); view.rerender(<PutawaySerialCapture resetKey="1" disabled onChoose={choose} />);
  expect(screen.queryByLabelText('Khung quét camera')).toBeNull(); await act(async () => { release(); });
  await waitFor(() => expect(state.stop).toHaveBeenCalled()); read('LATE-READ', 3); expect(choose).not.toHaveBeenCalled();
  expect((screen.getByRole('button', { name: 'Quét bằng camera' }) as HTMLButtonElement).disabled).toBe(true);
});

it('stops a pending camera request after unmount before camera permission resolves', async () => {
  let release!: () => void; state.camera.mockImplementation(() => new Promise<void>(resolve => { release = resolve; }));
  const choose = vi.fn(); const view = render(<PutawaySerialCapture resetKey="1" disabled={false} onChoose={choose} />);
  await openCamera(); view.unmount(); await act(async () => { release(); });
  await waitFor(() => expect(state.stop).toHaveBeenCalled()); read('LATE-READ', 3); expect(choose).not.toHaveBeenCalled();
});

it('releases the old camera on a new serial context and requires fresh consensus after rescan', async () => {
  const choose = vi.fn(); const rescan = vi.fn();
  const view = render(<PutawaySerialCapture resetKey="1" disabled={false} onChoose={choose} onRescan={rescan} />);
  await openCamera(); const oldCallback = state.callback!; read('OLD-SERIAL', 2);
  view.rerender(<PutawaySerialCapture resetKey="2" disabled={false} onChoose={choose} onRescan={rescan} />);
  expect(state.stop).toHaveBeenCalled(); expect(screen.queryByLabelText('Khung quét camera')).toBeNull();
  act(() => { for(let i = 0; i < 3; i++) oldCallback({ getText: () => 'OLD-SERIAL' }, null, { stop: state.stop }); });
  expect(choose).not.toHaveBeenCalled(); state.callback = null; await openCamera(); read('NEW-SERIAL', 2);
  expect(choose).not.toHaveBeenCalled(); fireEvent.click(screen.getByRole('button', { name: 'Quét lại' })); expect(rescan).toHaveBeenCalledOnce();
  read('NEW-SERIAL', 2); expect(choose).not.toHaveBeenCalled(); read('NEW-SERIAL');
  expect(choose).toHaveBeenCalledExactlyOnceWith('NEW-SERIAL');
});

it('explains denied camera permission while keeping manual or uploaded serial entry available', async () => {
  state.camera.mockRejectedValue(new DOMException('denied', 'NotAllowedError'));
  render(<PutawaySerialCapture resetKey="1" disabled={false} onChoose={vi.fn()} />); await openCamera();
  await screen.findByText('Bạn chưa cấp quyền camera. Cho phép camera trong trình duyệt hoặc tải ảnh/nhập tay.');
  expect(screen.getByRole('button', { name: 'Quét bằng camera' })).toBeTruthy(); expect(screen.getByText('Tải ảnh tem')).toBeTruthy();
  expect(screen.queryByLabelText('Khung quét camera')).toBeNull();
});
