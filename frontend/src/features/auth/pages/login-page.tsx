import { BoxMark } from '../../../shared/components/ui/box-mark';
import { LoginForm } from '../components/login-form';
import { LoginIllustration } from '../components/login-illustration';

export function LoginPage() {
  return (
    <div className="login-page">
      <header className="site-header"><span className="site-logo"><BoxMark /></span><span>EWMS</span></header>
      <main className="login-content">
        <section className="login-intro" aria-labelledby="intro-title">
          <p className="eyebrow">ELECTRONICS WAREHOUSE MANAGEMENT</p>
          <h1 id="intro-title">Mọi thứ.<br />Đúng chỗ.</h1>
          <p className="intro-description">Một không gian chung cho đơn mua hàng,<br className="desktop-break" /> nhận hàng và vận hành kho mỗi ngày.</p>
          <LoginIllustration />
        </section>
        <LoginForm />
      </main>
    </div>
  );
}
