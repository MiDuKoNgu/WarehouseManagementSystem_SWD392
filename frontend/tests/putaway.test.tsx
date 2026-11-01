import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { App } from '../src/app/app';
import type { PutawayCandidate, PutawaySerial } from '../src/features/inbound/putaway-api';

const user = { userId: 3, username: 'staff', fullName: 'Nhân viên Kho A', role: 'WAREHOUSE_STAFF', warehouseId: 1 };
const original: PutawaySerial = {
  serialItemId: 99, serialNumber: 'SN-AbC-99', productId: 17, stockState: 'IN_WAREHOUSE', itemCondition: 'GOOD', isQuarantined: false,
  currentWarehouseId: 1, currentLocationId: null, currentLocationPath: null, rowVersion: 'AAAAAAAAAAE=', warehouseName: 'Kho A',
  originalInboundId: 12, receiptCode: 'INB-012', receivedAt: '2026-11-01T04:00:00Z', hasActiveClaim: false, hasBlockingLock: false, inboundStatus: 'DONE',
  product: { productId: 17, productCode: 'TV-55', productName: 'Ti vi Samsung 55 inch', categoryId: 7, categoryName: 'Ti vi', brandId: 8, brandName: 'Samsung', status: 'ACTIVE', lengthCm: 130, widthCm: 30, heightCm: 85, grossWeightKg: 22 },
};
function location(reserve: boolean): PutawayCandidate {
  return { locationId: reserve ? 201 : 101, locationPath: `Kho A → ${reserve ? 'A' : 'B'} → Dãy 01 → Ô sàn 01`, locationType: 'FLOOR_SLOT', zoneType: reserve ? 'RESERVE' : 'STANDARD', isReserve: reserve, capacityMode: 'FLOOR_AREA', lengthCm: 200, widthCm: 120, heightCm: 220, totalAreaM2: 2.4, usedAreaM2: .4, availableAreaM2: 2, totalVolumeM3: null, usedVolumeM3: null, availableVolumeM3: null, maxWeightKg: 500, usedWeightKg: 22, remainingWeightKg: 478, occupantCount: 1, suggestionRank: reserve ? 2 : 1, isValid: true, reason: null };
}
const response = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
let serial: PutawaySerial; let placed: boolean; let conflict: boolean; let candidateList: PutawayCandidate[];
beforeEach(() => {
  serial = structuredClone(original); placed = false; conflict = false; candidateList = [location(false), location(true)];
  window.history.replaceState(null, '', '/warehouse/pending-putaway');
  vi.stubGlobal('fetch', vi.fn(async (path: string, init?: RequestInit) => {
    if (path === '/api/auth/me') return response(user);
    if (path === '/api/auth/csrf') return response({ token: 'putaway-csrf' });
    if (path.startsWith('/api/putaway/pending?')) return response({ items: placed ? [] : [serial], totalCount: placed ? 0 : 1, page: 1, pageSize: 20 });
    if (path.startsWith('/api/putaway/serials?')) return response(serial);
    if (path === `/api/putaway/${serial.serialItemId}/suggestions`) return response({ serial, candidates: candidateList, reason: null });
    if (path === '/api/putaway' && init?.method === 'POST') {
      if (conflict) return response({ message: 'Vị trí đã hết chỗ. Vui lòng tải lại gợi ý.' }, 409);
      placed = true;
      const body = JSON.parse(String(init.body)); const target = candidateList.find(c => c.locationId === body.targetLocationId)!;
      return response({ serialItemId: 99, serialNumber: serial.serialNumber, targetLocationId: target.locationId, locationPath: target.locationPath, occurredAt: '2026-11-01T10:00:00Z', actorUserId: 3, actorName: user.fullName, rowVersion: 'AAAAAAAAAAI=' });
    }
    throw new Error(`Unexpected ${init?.method || 'GET'} ${path}`);
  }));
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

async function chooseSerial() {
  await screen.findByRole('button', { name: /Chọn Serial SN-AbC-99/ });
  fireEvent.click(screen.getByRole('button', { name: /Chọn Serial SN-AbC-99/ }));
  await screen.findByRole('radio', { name: /Kho A → B/ });
}
function postCalls() { return vi.mocked(fetch).mock.calls.filter(([path, init]) => path === '/api/putaway' && init?.method === 'POST'); }

it('allows manual RESERVE selection and writes once after reviewing the location without requiring a second serial scan', async () => {
  render(<App />); await chooseSerial();
  expect((screen.getByRole('radio', { name: /Kho A → B/ }) as HTMLInputElement).checked).toBe(true);
  fireEvent.click(screen.getByRole('radio', { name: /Kho A → A/ }));
  fireEvent.click(screen.getByRole('button', { name: 'Xác nhận vị trí →' }));
  const dialog = screen.getByRole('dialog', { name: 'Xác nhận xếp kho' });
  expect(postCalls()).toHaveLength(0);
  expect(within(dialog).queryByLabelText('Quét lại Serial để xác nhận')).toBeNull();
  expect(within(dialog).queryByRole('textbox')).toBeNull();
  fireEvent.click(within(dialog).getByRole('button', { name: 'Xác nhận Putaway' }));
  await screen.findByText(/Đã xếp SN-AbC-99 tại Kho A → A/);
  expect(postCalls()).toHaveLength(1);
  const [, init] = postCalls()[0];
  expect(JSON.parse(String(init?.body))).toEqual({ serialItemId: 99, targetLocationId: 201, rowVersion: 'AAAAAAAAAAE=' });
  expect((init?.headers as Record<string, string>)['X-CSRF-TOKEN']).toBe('putaway-csrf');
  await screen.findByText('0 thiết bị chờ');
  expect(screen.queryByRole('button', { name: /Chọn Serial SN-AbC-99/ })).toBeNull();
  expect(document.activeElement).toBe(screen.getByLabelText('Serial cần xếp kho'));
});

it('retains the chosen location after a 409 and requires a fresh authoritative state before another attempt', async () => {
  conflict = true;
  render(<App />); await chooseSerial(); fireEvent.click(screen.getByRole('radio', { name: /Kho A → A/ }));
  fireEvent.click(screen.getByRole('button', { name: 'Xác nhận vị trí →' }));
  let dialog = screen.getByRole('dialog');
  fireEvent.click(within(dialog).getByRole('button', { name: 'Xác nhận Putaway' }));
  await within(dialog).findByText('Vị trí đã hết chỗ. Vui lòng tải lại gợi ý.');
  expect(within(dialog).queryByRole('button', { name: 'Xác nhận Putaway' })).toBeNull();
  serial = { ...serial, rowVersion: 'AAAAAAAAAAM=' };
  fireEvent.click(within(dialog).getByRole('button', { name: 'Tải lại gợi ý' }));
  await waitFor(() => expect((screen.getByRole('button', { name: 'Xác nhận vị trí →' }) as HTMLButtonElement).disabled).toBe(false));
  expect((screen.getByRole('radio', { name: /Kho A → A/ }) as HTMLInputElement).checked).toBe(true);
  expect(postCalls()).toHaveLength(1);
  conflict = false;
  fireEvent.click(screen.getByRole('button', { name: 'Xác nhận vị trí →' })); dialog = screen.getByRole('dialog');
  fireEvent.click(within(dialog).getByRole('button', { name: 'Xác nhận Putaway' }));
  await screen.findByText(/Đã xếp SN-AbC-99 tại/);
  expect(JSON.parse(String(postCalls()[1][1]?.body)).rowVersion).toBe('AAAAAAAAAAM=');
});

it('reports the current location on a repeated scan without requesting suggestions or writing again', async () => {
  serial.currentLocationId = 101; serial.currentLocationPath = location(false).locationPath;
  render(<App />); await screen.findByRole('heading', { name: serial.serialNumber });
  fireEvent.change(screen.getByLabelText('Serial cần xếp kho'), { target: { value: serial.serialNumber } });
  fireEvent.submit(screen.getByRole('button', { name: 'Tra cứu Serial' }).closest('form')!);
  await screen.findByText(/Serial đã được đặt tại/);
  expect(screen.queryByRole('button', { name: 'Xác nhận vị trí →' })).toBeNull();
  expect(postCalls()).toHaveLength(0);
  expect(vi.mocked(fetch).mock.calls.some(([path]) => path === '/api/putaway/99/suggestions')).toBe(false);
});

it('shows quarantined DAMAGED condition and keeps confirmation payload limited to serial, target and version', async () => {
  serial.itemCondition = 'DAMAGED'; serial.isQuarantined = true; candidateList = [location(true)];
  render(<App />);
  fireEvent.change(await screen.findByLabelText('Serial cần xếp kho'), { target: { value: serial.serialNumber } });
  fireEvent.submit(screen.getByRole('button', { name: 'Tra cứu Serial' }).closest('form')!);
  await screen.findByRole('radio', { name: /Kho A → A/ });
  expect(screen.getByText(/Đang cách ly. Putaway giữ nguyên cách ly/)).toBeTruthy();
  expect(screen.queryByText('Ưu tiên STANDARD', { exact: true })).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Xác nhận vị trí →' }));
  const dialog = screen.getByRole('dialog');
  expect(within(dialog).getByText('Thiết bị tiếp tục được cách ly sau khi xếp kho.')).toBeTruthy();
  fireEvent.click(within(dialog).getByRole('button', { name: 'Xác nhận Putaway' }));
  await screen.findByText(/Đã xếp SN-AbC-99 tại/);
  expect(Object.keys(JSON.parse(String(postCalls()[0][1]?.body))).sort()).toEqual(['rowVersion', 'serialItemId', 'targetLocationId']);
});

it('sends search and pagination to the server and displays volume with m³ for tiers', async () => {
  const previous = vi.mocked(fetch).getMockImplementation()!;
  vi.stubGlobal('fetch', vi.fn(async (path: string, init?: RequestInit) => path.startsWith('/api/putaway/pending?') ? response({ items: [serial], totalCount: 45, page: Number(new URLSearchParams(path.split('?')[1]).get('page')), pageSize: 20 }) : previous(path, init)));
  candidateList = [{ ...location(false), locationType: 'TIER', capacityMode: 'VOLUME', totalAreaM2: null, usedAreaM2: null, availableAreaM2: null, totalVolumeM3: 4.455, usedVolumeM3: .5, availableVolumeM3: 3.955 }];
  render(<App />); await screen.findByText('Trang 1 / 3');
  fireEvent.click(screen.getByRole('button', { name: 'Sau →' })); await screen.findByText('Trang 2 / 3');
  expect(vi.mocked(fetch).mock.calls.some(([path]) => path.includes('/api/putaway/pending?') && path.includes('page=2'))).toBe(true);
  fireEvent.change(screen.getByLabelText('Tìm Serial / sản phẩm / phiếu nhập'), { target: { value: 'TV 55' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Tìm', exact: true }).closest('form')!);
  await screen.findByText('Trang 1 / 3');
  expect(vi.mocked(fetch).mock.calls.some(([path]) => path.includes('search=TV+55') && path.includes('page=1'))).toBe(true);
  await chooseSerial();
  expect(screen.getByText('Thể tích còn lại')).toBeTruthy();
  expect(screen.getByText('3,955 m³')).toBeTruthy();
  expect(screen.queryByText('Diện tích còn lại')).toBeNull();
});

it('preserves reserved URL characters in scanner input and states honestly when claim/stocktake checks are unavailable', async () => {
  serial.serialNumber = 'SN/A?x=%#1';
  render(<App />);
  fireEvent.change(await screen.findByLabelText('Serial cần xếp kho'), { target: { value: serial.serialNumber } });
  fireEvent.submit(screen.getByRole('button', { name: 'Tra cứu Serial' }).closest('form')!);
  await screen.findByRole('radio', { name: /Kho A → B/ });
  const [url] = vi.mocked(fetch).mock.calls.find(([path]) => path.startsWith('/api/putaway/serials?'))!;
  expect(new URLSearchParams(String(url).split('?')[1]).get('serialNumber')).toBe(serial.serialNumber);
  expect(screen.getByText('Thông tin khóa kiểm kê chưa khả dụng')).toBeTruthy();
  expect(screen.queryByText('Chưa đặt chỗ')).toBeNull();
  expect(screen.queryByText('Không bị khóa kiểm kê')).toBeNull();
});

function rackLocation(locationId: number, zone: string, row: string, rack: string, tier: string): PutawayCandidate {
  return {
    ...location(false), locationId, locationPath: `Kho A → ${zone} → Dãy ${row} → Kệ ${rack} → Tầng ${tier}`,
    locationType: 'TIER', capacityMode: 'VOLUME', lengthCm: 270, widthCm: 110, heightCm: 150,
    totalAreaM2: null, usedAreaM2: null, availableAreaM2: null, totalVolumeM3: 4.455,
    usedVolumeM3: .5, availableVolumeM3: 3.955, suggestionRank: locationId,
  };
}

function withRackLocations() {
  candidateList = [location(false), location(true), rackLocation(301, 'C', '02', '03', '01'),
    rackLocation(302, 'C', '02', '03', '02'), rackLocation(303, 'C', '02', '13', '01'),
    rackLocation(401, 'D', '01', '03', '01'), { ...rackLocation(999, 'C', '02', '03', '03'), isValid: false, reason: 'Vị trí không hợp lệ' }];
}

it('removes the STANDARD helper sentence and immediately narrows combined accent-insensitive zone/rack searches', async () => {
  withRackLocations(); render(<App />); await chooseSerial();
  expect(screen.queryByText(/STANDARD được ưu tiên\. Có thể chọn RESERVE/)).toBeNull();
  const search = screen.getByLabelText('Tìm vị trí');
  expect(search.getAttribute('placeholder')).toBe('Ví dụ: zone C, kệ 3');
  fireEvent.change(search, { target: { value: 'ZONE c, KE3' } });
  const radios = screen.getAllByRole('radio');
  expect(radios).toHaveLength(2);
  expect(screen.getByRole('radio', { name: /C → Dãy 02 → Kệ 03 → Tầng 01/ })).toBeTruthy();
  expect(screen.getByRole('radio', { name: /C → Dãy 02 → Kệ 03 → Tầng 02/ })).toBeTruthy();
  expect(screen.queryByRole('radio', { name: /Kệ 13/ })).toBeNull();
  expect(screen.queryByRole('radio', { name: /Kệ 03 → Tầng 03/ })).toBeNull();
  expect(screen.queryByRole('radio', { name: /Kho A → B/ })).toBeNull();
  expect(screen.queryByRole('radio', { name: /Kho A → D/ })).toBeNull();
  expect(postCalls()).toHaveLength(0);
});

it('keeps the chosen location and confirmation preview unchanged when a location filter hides it', async () => {
  withRackLocations(); render(<App />); await chooseSerial();
  fireEvent.click(screen.getByRole('radio', { name: /C → Dãy 02 → Kệ 03 → Tầng 02/ }));
  fireEvent.change(screen.getByLabelText('Tìm vị trí'), { target: { value: 'zone D' } });
  expect(screen.getAllByRole('radio')).toHaveLength(1);
  expect(screen.queryByRole('radio', { name: /C → Dãy 02/ })).toBeNull();
  expect((screen.getByRole('radio', { name: /Kho A → D/ }) as HTMLInputElement).checked).toBe(false);
  expect((screen.getByRole('button', { name: 'Xác nhận vị trí →' }) as HTMLButtonElement).disabled).toBe(true);
  expect(screen.getByText('Vị trí đã chọn nằm ngoài bộ lọc. Chọn một vị trí trong kết quả để xác nhận.')).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Xóa bộ lọc' }));
  expect((screen.getByRole('radio', { name: /C → Dãy 02 → Kệ 03 → Tầng 02/ }) as HTMLInputElement).checked).toBe(true);
  fireEvent.click(screen.getByRole('button', { name: 'Xác nhận vị trí →' }));
  const dialog = screen.getByRole('dialog', { name: 'Xác nhận xếp kho' });
  expect(dialog.textContent).toContain('Kho A → C → Dãy 02 → Kệ 03 → Tầng 02');
  expect(dialog.textContent).not.toContain('Kho A → D');
  expect(postCalls()).toHaveLength(0);
  fireEvent.click(within(dialog).getByRole('button', { name: 'Quay lại' }));
  expect((screen.getByRole('radio', { name: /C → Dãy 02 → Kệ 03 → Tầng 02/ }) as HTMLInputElement).checked).toBe(true);
});

it('shows no matching locations without replacing the selection and clears all location filters', async () => {
  withRackLocations(); render(<App />); await chooseSerial();
  fireEvent.change(screen.getByLabelText('Tìm vị trí'), { target: { value: 'zone Z ke 99' } });
  expect(screen.queryAllByRole('radio')).toHaveLength(0);
  expect((screen.getByRole('button', { name: 'Xác nhận vị trí →' }) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByRole('button', { name: 'Xóa bộ lọc' }));
  expect((screen.getByLabelText('Tìm vị trí') as HTMLInputElement).value).toBe('');
  expect(screen.getAllByRole('radio')).toHaveLength(6);
  expect((screen.getByRole('radio', { name: /Kho A → B/ }) as HTMLInputElement).checked).toBe(true);
});

it('combines structured filters and resets child filters when their parent zone changes', async () => {
  withRackLocations(); render(<App />); await chooseSerial();
  const zone = screen.getByRole('combobox', { name: 'Zone', exact: true }) as HTMLSelectElement;
  const row = screen.getByRole('combobox', { name: 'Dãy', exact: true }) as HTMLSelectElement;
  const rack = screen.getByRole('combobox', { name: 'Kệ', exact: true }) as HTMLSelectElement;
  const level = screen.getByRole('combobox', { name: 'Tầng / Ô sàn', exact: true }) as HTMLSelectElement;
  const initial = { zone: zone.value, row: row.value, rack: rack.value, level: level.value };
  fireEvent.change(zone, { target: { value: within(zone).getByRole('option', { name: 'C', exact: true }).getAttribute('value') } });
  expect(screen.getAllByRole('radio')).toHaveLength(3);
  fireEvent.change(row, { target: { value: within(row).getByRole('option', { name: 'Dãy 02', exact: true }).getAttribute('value') } });
  fireEvent.change(rack, { target: { value: within(rack).getByRole('option', { name: 'Kệ 03', exact: true }).getAttribute('value') } });
  expect(screen.getAllByRole('radio')).toHaveLength(2);
  fireEvent.change(level, { target: { value: within(level).getByRole('option', { name: 'Tầng 02', exact: true }).getAttribute('value') } });
  expect(screen.getAllByRole('radio')).toHaveLength(1);
  expect(screen.getByRole('radio', { name: /C → Dãy 02 → Kệ 03 → Tầng 02/ })).toBeTruthy();
  fireEvent.change(zone, { target: { value: within(zone).getByRole('option', { name: 'D', exact: true }).getAttribute('value') } });
  expect(row.value).toBe(initial.row); expect(rack.value).toBe(initial.rack); expect(level.value).toBe(initial.level);
  expect(screen.getAllByRole('radio')).toHaveLength(1);
  expect(screen.getByRole('radio', { name: /Kho A → D/ })).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Xóa bộ lọc' }));
  expect(zone.value).toBe(initial.zone); expect(row.value).toBe(initial.row);
  expect(rack.value).toBe(initial.rack); expect(level.value).toBe(initial.level);
  expect(screen.getAllByRole('radio')).toHaveLength(6);
});

it('resets location filters when looking up a new serial instead of hiding its suggestions', async () => {
  withRackLocations(); render(<App />); await chooseSerial();
  const filterNames = ['Zone', 'Dãy', 'Kệ', 'Tầng / Ô sàn'];
  const initialValues = filterNames.map(name => (screen.getByRole('combobox', { name, exact: true }) as HTMLSelectElement).value);
  fireEvent.change(screen.getByLabelText('Tìm vị trí'), { target: { value: 'zone C ke3' } });
  expect(screen.getAllByRole('radio')).toHaveLength(2);
  for (const [name, optionName] of [['Zone', 'C'], ['Dãy', 'Dãy 02'], ['Kệ', 'Kệ 03'], ['Tầng / Ô sàn', 'Tầng 02']]) {
    const select = screen.getByRole('combobox', { name, exact: true });
    fireEvent.change(select, { target: { value: (within(select).getByRole('option', { name: optionName, exact: true }) as HTMLOptionElement).value } });
  }
  expect(screen.getAllByRole('radio')).toHaveLength(1);
  serial = { ...serial, serialItemId: 100, serialNumber: 'NEW-SERIAL-100' };
  fireEvent.change(screen.getByLabelText('Serial cần xếp kho'), { target: { value: serial.serialNumber } });
  fireEvent.submit(screen.getByRole('button', { name: 'Tra cứu Serial' }).closest('form')!);
  await screen.findByRole('heading', { name: 'NEW-SERIAL-100' });
  await screen.findByRole('radio', { name: /Kho A → B/ });
  expect((screen.getByLabelText('Tìm vị trí') as HTMLInputElement).value).toBe('');
  expect(filterNames.map(name => (screen.getByRole('combobox', { name, exact: true }) as HTMLSelectElement).value)).toEqual(initialValues);
  expect(screen.getAllByRole('radio')).toHaveLength(6);
  expect((screen.getByRole('radio', { name: /Kho A → B/ }) as HTMLInputElement).checked).toBe(true);
});
