export class ApiError extends Error {
  constructor(public status: number, message: string) { super(message); }
}

type RequestOptions = RequestInit & { timeoutMs?: number };

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { timeoutMs = 15000, signal, ...init } = options;
  const controller = new AbortController();
  let timedOut = false;
  const timer = setTimeout(() => { timedOut = true; controller.abort(); }, timeoutMs);
  const abort = () => controller.abort();
  if (signal?.aborted) controller.abort();
  else signal?.addEventListener('abort', abort, { once: true });
  try {
    const response = await fetch(path, { ...init, signal: controller.signal, credentials: 'same-origin', cache: 'no-store' });
    if (!response.ok) {
      const body = await response.json().catch(() => null);
      const fallback = response.status === 401 ? 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.'
        : response.status === 403 ? 'Bạn không có quyền truy cập trang này.'
        : response.status === 400 ? 'Thông tin nhập chưa hợp lệ. Vui lòng kiểm tra lại.'
        : response.status === 404 ? 'Không tìm thấy dữ liệu yêu cầu.'
        : response.status === 409 ? 'Dữ liệu đã thay đổi hoặc bản xem trước đã hết hạn. Vui lòng kiểm tra lại.'
        : 'Dịch vụ đang không khả dụng. Vui lòng thử lại sau.';
      const message = typeof body?.message === 'string' && body.message.trim() ? body.message : fallback;
      const errors: unknown[] = Array.isArray(body?.errors) ? body.errors
        : body?.errors && typeof body.errors === 'object' ? Object.values(body.errors).flat() : [];
      const details = errors.filter((value): value is string => typeof value === 'string' && !!value.trim() && value !== message);
      throw new ApiError(response.status, [message, ...new Set(details)].join('\n'));
    }
    return response.status === 204 ? undefined as T : await response.json();
  } catch (reason) {
    if (timedOut) throw new ApiError(0, 'Máy chủ phản hồi quá lâu. Vui lòng kiểm tra backend và thử lại.');
    if (reason instanceof ApiError) throw reason;
    if (signal?.aborted) throw reason;
    throw new ApiError(0, 'Không thể kết nối máy chủ. Vui lòng kiểm tra kết nối và thử lại.');
  } finally {
    clearTimeout(timer);
    signal?.removeEventListener('abort', abort);
  }
}
