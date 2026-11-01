import { useCallback } from 'react';
import { useAuth } from '../../../app/providers/auth-provider';
import { ApiError } from '../../../shared/lib/http-client';

export function usePoError() {
  const { refresh } = useAuth();
  return useCallback((reason: unknown) => {
    if (reason instanceof ApiError && reason.status === 401) void refresh();
    return reason instanceof Error ? reason.message : 'Có lỗi xảy ra. Vui lòng thử lại.';
  }, [refresh]);
}
