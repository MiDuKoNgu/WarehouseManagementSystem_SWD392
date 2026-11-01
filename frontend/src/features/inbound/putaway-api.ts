import { request } from '../../shared/lib/http-client';

export type PutawayProduct = {
  productId: number; productCode: string; productName: string;
  categoryId: number; categoryName: string; brandId: number; brandName: string; status: string;
  lengthCm: number | null; widthCm: number | null; heightCm: number | null; grossWeightKg: number | null;
};
export type PutawaySerial = {
  serialItemId: number; serialNumber: string; productId: number; stockState: string;
  itemCondition: string; isQuarantined: boolean; currentWarehouseId: number;
  currentLocationId: number | null; currentLocationPath: string | null; rowVersion: string;
  product: PutawayProduct | null; warehouseName: string;
  originalInboundId: number | null; receiptCode: string | null; receivedAt: string | null;
  hasActiveClaim: boolean; hasBlockingLock: boolean; inboundStatus: string | null; originType?: string | null;
  claimChecksAvailable?: boolean; stocktakeChecksAvailable?: boolean;
};
export type PutawayCandidate = {
  locationId: number; locationPath: string; locationType: string; zoneType: string;
  isReserve: boolean; capacityMode: string; lengthCm: number | null; widthCm: number | null; heightCm: number | null;
  totalAreaM2: number | null; usedAreaM2: number | null; availableAreaM2: number | null;
  totalVolumeM3: number | null; usedVolumeM3: number | null; availableVolumeM3: number | null;
  maxWeightKg: number | null; usedWeightKg: number; remainingWeightKg: number | null;
  occupantCount: number; suggestionRank: number; isValid: boolean; reason: string | null;
};
export type PutawayPending = { items: PutawaySerial[]; totalCount: number; page: number; pageSize: number };
export type PutawaySuggestions = { serial: PutawaySerial; candidates: PutawayCandidate[]; reason: string | null };
export type PutawayResult = {
  serialItemId: number; serialNumber: string; targetLocationId: number; locationPath: string;
  occurredAt: string; actorUserId: number; actorName: string | null; rowVersion: string;
};
const base = '/api/putaway';
export const putawayApi = {
  pending: (search: string, page: number, pageSize: number, signal?: AbortSignal) => {
    const query = new URLSearchParams({ search, page: String(page), pageSize: String(pageSize) });
    return request<PutawayPending>(`${base}/pending?${query}`, { signal });
  },
  serial: (serialNumber: string, signal?: AbortSignal) => request<PutawaySerial>(`${base}/serials?${new URLSearchParams({ serialNumber })}`, { signal }),
  suggestions: (serialItemId: number, signal?: AbortSignal) => request<PutawaySuggestions>(`${base}/${serialItemId}/suggestions`, { signal }),
  confirm: async (serialItemId: number, targetLocationId: number, rowVersion: string) => {
    const { token } = await request<{ token: string }>('/api/auth/csrf');
    return request<PutawayResult>(base, {
      method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
      body: JSON.stringify({ serialItemId, targetLocationId, rowVersion }), timeoutMs: 60000,
    });
  },
};
