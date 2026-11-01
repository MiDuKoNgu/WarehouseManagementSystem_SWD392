import { useCallback } from 'react';
import { useAuth } from '../../../app/providers/auth-provider';
import { ApiError } from '../../../shared/lib/http-client';

export function useAuditError() {
  const { refresh } = useAuth();
  return useCallback((reason: unknown) => {
    if (reason instanceof ApiError && reason.status === 401) void refresh();
    return reason instanceof Error ? reason.message : 'Chưa thể đọc lịch sử. Vui lòng thử lại.';
  }, [refresh]);
}
export function AuditError({ message, retry }: { message: string; retry?: () => void }) {
  return <div className="audit-alert" role="alert"><p>{message}</p>{retry && <button className="utility-button" onClick={retry}>Thử lại</button>}</div>;
}
export function AuditStatus({ value }: { value: string }) {
  return <span className={`audit-status ${value === 'FAILED' ? 'is-failed' : ''}`}>{value === 'SUCCESS' ? 'Thành công' : value === 'FAILED' ? 'Thất bại' : value}</span>;
}
