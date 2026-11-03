import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { useState } from 'react';
import { afterEach, expect, it, vi } from 'vitest';
import { AuthProvider, useAuth } from '../src/app/providers/auth-provider';

const reply = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status });
function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>(complete => { resolve = complete; });
  return { promise, resolve };
}
function Probe() {
  const { loading, user, login } = useAuth();
  const [failure, setFailure] = useState('');
  return <><p data-testid="session-state">{loading ? 'loading' : 'ready'}:{user?.username || 'anonymous'}</p>
    <button onClick={() => void login('buyer', 'wrong-password', false).catch(() => setFailure('Login rejected'))}>Login</button>
    {failure && <p>{failure}</p>}</>;
}
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('finishes loading after a failed login invalidates the pending initial session check', async () => {
  const session = deferred<Response>();
  vi.stubGlobal('fetch', vi.fn(async (path: string) => {
    if (path === '/api/auth/me') return session.promise;
    if (path === '/api/auth/csrf') return reply({ token: 'csrf' });
    if (path === '/api/auth/login') return reply({ message: 'Wrong password' }, 401);
    throw new Error(`Unexpected ${path}`);
  }));
  render(<AuthProvider><Probe /></AuthProvider>);
  fireEvent.click(screen.getByRole('button', { name: 'Login' }));
  try {
    await screen.findByText('Login rejected');
    expect(screen.getByTestId('session-state').textContent).toBe('ready:anonymous');
  } finally { session.resolve(reply({}, 401)); }
});

it('does not let an older failed login finish a newer pending session refresh', async () => {
  const session = deferred<Response>(); const loginResponse = deferred<Response>();
  vi.stubGlobal('fetch', vi.fn(async (path: string) => {
    if (path === '/api/auth/me') return session.promise;
    if (path === '/api/auth/csrf') return reply({ token: 'csrf' });
    if (path === '/api/auth/login') return loginResponse.promise;
    throw new Error(`Unexpected ${path}`);
  }));
  render(<AuthProvider><Probe /></AuthProvider>);
  fireEvent.click(screen.getByRole('button', { name: 'Login' }));
  await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([path]) => path === '/api/auth/login')).toBe(true));
  fireEvent(window, new Event('focus'));
  loginResponse.resolve(reply({ message: 'Wrong password' }, 401));
  try {
    await screen.findByText('Login rejected');
    expect(screen.getByTestId('session-state').textContent).toBe('loading:anonymous');
  } finally { session.resolve(reply({}, 401)); }
  await waitFor(() => expect(screen.getByTestId('session-state').textContent).toBe('ready:anonymous'));
});
