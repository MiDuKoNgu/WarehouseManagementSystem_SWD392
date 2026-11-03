import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from '../src/app/app';

const buyer = { userId: 2, username: 'buyer', fullName: 'Buyer Test', role: 'PURCHASING_STAFF', warehouseId: null };
const reply = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

beforeEach(() => {
  window.history.replaceState(null, '', '/purchasing');
  vi.stubGlobal('fetch', vi.fn(async (path: string) => {
    if (path === '/api/auth/me') return reply(buyer);
    if (path === '/api/workspaces/purchasing') return reply({ user: buyer });
    if (path === '/api/purchase-orders') return reply([]);
    throw new Error(`Unexpected API ${path}`);
  }));
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

describe('internal navigation and session lifetime', () => {
  it('opens the workspace when login finishes before the initial session check', async () => {
    window.history.replaceState(null, '', '/login');
    let finishSession!: (response: Response) => void;
    const session = new Promise<Response>(resolve => { finishSession = resolve; });
    vi.stubGlobal('fetch', vi.fn(async (path: string) => {
      if (path === '/api/auth/me') return session;
      if (path === '/api/auth/csrf') return reply({ token: 'test-csrf' });
      if (path === '/api/auth/login') return reply(buyer);
      if (path === '/api/workspaces/purchasing') return reply({ user: buyer });
      throw new Error(`Unexpected API ${path}`);
    }));
    render(<App />);
    fireEvent.change(screen.getByLabelText('Tài khoản'), { target: { value: 'buyer' } });
    fireEvent.change(screen.getByLabelText('Mật khẩu'), { target: { value: 'test-password' } });
    fireEvent.submit(screen.getByRole('button', { name: 'Đăng nhập', exact: false }).closest('form')!);
    try {
      await screen.findByRole('heading', { name: 'Mua đúng hàng. Sẵn sàng nhận.' });
    } finally { finishSession(reply({}, 401)); }
    await waitFor(() => expect(screen.queryByText('Đang kiểm tra phiên đăng nhập…')).toBeNull());
    expect(window.location.pathname).toBe('/purchasing');
  });
  it.each(['/', '/login'])('opens the login page at %s even with an existing session', async (path) => {
    window.history.replaceState(null, '', path);
    render(<App />);
    await screen.findByRole('button', { name: 'Đăng nhập', exact: false });
    await waitFor(() => expect(window.location.pathname).toBe('/login'));
    fireEvent(window, new Event('focus'));
    await waitFor(() => expect(vi.mocked(fetch).mock.calls.filter(([url]) => url === '/api/auth/me').length).toBeGreaterThan(1));
    expect(window.location.pathname).toBe('/login');
    expect(vi.mocked(fetch).mock.calls.some(([url]) => url === '/api/workspaces/purchasing')).toBe(false);
  });

  it('navigates to the correct workspace after submitting login', async () => {
    window.history.replaceState(null, '', '/login');
    vi.stubGlobal('fetch', vi.fn(async (path: string) => {
      if (path === '/api/auth/csrf') return reply({ token: 'test-csrf' });
      if (path === '/api/auth/me' || path === '/api/auth/login') return reply(buyer);
      if (path === '/api/workspaces/purchasing') return reply({ user: buyer });
      throw new Error(`Unexpected API ${path}`);
    }));
    render(<App />);
    const submit = await screen.findByRole('button', { name: 'Đăng nhập', exact: false });
    fireEvent.change(screen.getByLabelText('Tài khoản'), { target: { value: 'buyer' } });
    fireEvent.change(screen.getByLabelText('Mật khẩu'), { target: { value: 'test-password' } });
    fireEvent.submit(submit.closest('form')!);
    await waitFor(() => expect(window.location.pathname).toBe('/purchasing'));
    expect(vi.mocked(fetch).mock.calls.some(([url]) => url === '/api/auth/login')).toBe(true);
  });

  it('keeps authenticated state when moving between real overview, PO list and import pages', async () => {
    render(<App />);
    await screen.findByRole('heading', { name: 'Mua đúng hàng. Sẵn sàng nhận.' });
    const sessionCalls = () => vi.mocked(fetch).mock.calls.filter(([path]) => path === '/api/auth/me').length;
    const initialCalls = sessionCalls();
    fireEvent.click(screen.getByRole('link', { name: 'Đơn mua hàng' }));
    await screen.findByRole('heading', { name: 'Đơn mua hàng' });
    expect(window.location.pathname).toBe('/purchasing/purchase-orders');
    fireEvent.click(screen.getByRole('link', { name: 'Import Excel', exact: false }));
    await screen.findByRole('heading', { name: 'Một file. Một đơn mua.' });
    expect(window.location.pathname).toBe('/purchasing/purchase-orders/import');
    fireEvent.click(screen.getByRole('link', { name: 'Tổng quan', exact: false }));
    await screen.findByRole('heading', { name: 'Mua đúng hàng. Sẵn sàng nhận.' });
    expect(sessionCalls()).toBe(initialCalls);
    expect(screen.queryByText('Đang kiểm tra phiên đăng nhập…')).toBeNull();
  });

  it('continues to deny a purchasing user access to the admin route', async () => {
    window.history.replaceState(null, '', '/admin');
    render(<App />);
    await screen.findByRole('heading', { name: 'Bạn đang ở nhầm không gian.' });
    fireEvent.click(screen.getByRole('link', { name: 'Về trang của tôi →' }));
    await waitFor(() => expect(window.location.pathname).toBe('/purchasing'));
    await screen.findByRole('heading', { name: 'Mua đúng hàng. Sẵn sàng nhận.' });
  });
});
