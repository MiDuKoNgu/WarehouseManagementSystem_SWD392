import { request } from '../../../shared/lib/http-client';
import type { UserProfile } from '../types/auth';

async function post<T>(path: string, body?: unknown): Promise<T> {
  const { token } = await request<{ token: string }>('/api/auth/csrf');
  return request<T>(path, {
    method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
}

export const authApi = {
  me: () => request<UserProfile>('/api/auth/me', { timeoutMs: 8000 }),
  login: (username: string, password: string, rememberMe: boolean) =>
    post<UserProfile>('/api/auth/login', { username, password, rememberMe }),
  logout: () => post<void>('/api/auth/logout'),
  workspace: (name: string) => request<{ user: UserProfile }>(`/api/workspaces/${name}`),
};
