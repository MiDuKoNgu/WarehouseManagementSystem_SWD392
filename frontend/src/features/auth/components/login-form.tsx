import { useState, type FormEvent } from 'react';
import { BoxMark } from '../../../shared/components/ui/box-mark';
import { useAuth } from '../../../app/providers/auth-provider';
import { useNavigate } from 'react-router-dom';
import { rolePaths } from '../types/auth';

export function LoginForm() {
  const [showPassword, setShowPassword] = useState(false);
  const [message, setMessage] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const { login } = useAuth();
  const navigate = useNavigate();

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting) return;
    const form = new FormData(event.currentTarget);
    setSubmitting(true);
    setMessage('');
    try {
      const profile = await login(String(form.get('username')).trim(), String(form.get('password')), form.get('remember') === 'on');
      navigate(rolePaths[profile.role], { replace: true });
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : 'Đăng nhập không thành công. Vui lòng thử lại.');
    } finally { setSubmitting(false); }
  }

  return (
    <section className="login-panel" aria-labelledby="login-title">
      <div className="form-brand"><BoxMark /></div>
      <h2 id="login-title">Chào mừng trở lại.</h2>
      <p className="login-subtitle">Đăng nhập để tiếp tục công việc của bạn.</p>
      <form onSubmit={handleSubmit} onChange={() => setMessage('')} aria-busy={submitting}>
        <div className="form-field">
          <label htmlFor="username">Tài khoản</label>
          <input id="username" name="username" autoComplete="username" maxLength={100} required autoFocus disabled={submitting} />
        </div>
        <div className="form-field">
          <label htmlFor="password">Mật khẩu</label>
          <div className="password-field">
            <input id="password" name="password" type={showPassword ? 'text' : 'password'} autoComplete="current-password" maxLength={1024} required disabled={submitting} />
            <button className="password-toggle" type="button" onClick={() => setShowPassword(!showPassword)} aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} aria-pressed={showPassword}>
              <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12Z" stroke="currentColor" strokeWidth="1.7"/><circle cx="12" cy="12" r="3" stroke="currentColor" strokeWidth="1.7"/>{showPassword && <path d="m3 3 18 18" stroke="currentColor" strokeWidth="1.7"/>}</svg>
            </button>
          </div>
        </div>
        <label className="remember-field"><input type="checkbox" name="remember" /> <span>Ghi nhớ đăng nhập</span></label>
        <button className="login-button" type="submit" disabled={submitting}>{submitting ? 'Đang đăng nhập…' : 'Đăng nhập'} <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M4 12h15m-6-6 6 6-6 6" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"/></svg></button>
        {message && <p className="form-message" role="alert">{message}</p>}
      </form>
    </section>
  );
}
