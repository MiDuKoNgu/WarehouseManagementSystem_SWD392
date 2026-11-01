import { afterEach, expect, it, vi } from 'vitest';
import { ApiError, request } from '../src/shared/lib/http-client';

afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals(); });

it('stops waiting and reports a timeout when the server never responds', async () => {
  vi.useFakeTimers();
  vi.stubGlobal('fetch', vi.fn((_path, options: RequestInit) => new Promise((_resolve, reject) => {
    options.signal?.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')));
  })));
  const pending = request('/api/auth/me', { timeoutMs: 8000 });
  const check = expect(pending).rejects.toMatchObject({ status: 0, message: 'Máy chủ phản hồi quá lâu. Vui lòng kiểm tra backend và thử lại.' });
  await vi.advanceTimersByTimeAsync(8000);
  await check;
});

it('preserves server authentication failures instead of treating them as network errors', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ message: 'Phiên hết hạn' }), { status: 401 })));
  await expect(request('/api/auth/me')).rejects.toEqual(new ApiError(401, 'Phiên hết hạn'));
});
