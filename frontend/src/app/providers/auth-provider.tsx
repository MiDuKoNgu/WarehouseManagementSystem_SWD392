import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { authApi } from '../../features/auth/api/auth-api';
import type { UserProfile } from '../../features/auth/types/auth';
import { ApiError } from '../../shared/lib/http-client';

interface AuthContextValue {
  user: UserProfile | null;
  loading: boolean;
  error: string;
  refresh: () => Promise<void>;
  login: (username: string, password: string, remember: boolean) => Promise<UserProfile>;
  logout: () => Promise<void>;
}
const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const generation = useRef(0);
  const refresh = useCallback(async () => {
    const version = ++generation.current;
    try {
      const profile = await authApi.me();
      if (version === generation.current) { setUser(profile); setError(''); }
    } catch (reason) {
      if (version !== generation.current) return;
      if (reason instanceof ApiError && reason.status === 401) { setUser(null); setError(''); }
      else setError(reason instanceof Error ? reason.message : 'Không thể kiểm tra phiên đăng nhập.');
    } finally { if (version === generation.current) setLoading(false); }
  }, []);

  useEffect(() => {
    void refresh();
    const onFocus = () => { void refresh(); };
    window.addEventListener('focus', onFocus);
    return () => { generation.current++; window.removeEventListener('focus', onFocus); };
  }, [refresh]);

  async function login(username: string, password: string, remember: boolean) {
    const version = ++generation.current;
    try {
      const profile = await authApi.login(username, password, remember);
      generation.current++;
      setUser(profile);
      setLoading(false);
      setError('');
      return profile;
    } catch (reason) {
      if (version === generation.current) setLoading(false);
      throw reason;
    }
  }
  async function logout() {
    await authApi.logout();
    generation.current++;
    setUser(null);
    setLoading(false);
    setError('');
  }
  return <AuthContext.Provider value={{ user, loading, error, refresh, login, logout }}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('AuthProvider is missing');
  return context;
}
