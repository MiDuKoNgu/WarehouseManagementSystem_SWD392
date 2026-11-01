export function LoginIllustration() {
  return (
    <div className="login-illustration" aria-hidden="true">
      <div className="illustration-card purchase-card">
        <span className="illustration-badge document-badge">
          <svg viewBox="0 0 32 32" fill="none"><path d="M10 5h10l5 5v19H10Z" stroke="currentColor" strokeWidth="2"/><path d="M19 5v7h6M13 17h8m-8 5h8" stroke="currentColor" strokeWidth="2"/></svg>
        </span>
        <strong>Đơn mua hàng</strong>
        <div className="paper-line" /><div className="paper-line short" />
        <p><span className="status-dot" /> Sẵn sàng nhận hàng</p>
      </div>
      <div className="illustration-card stock-card">
        <span className="illustration-badge stock-badge">
          <svg viewBox="0 0 32 32" fill="none"><path d="m16 4 12 7v14l-12 7L4 25V11Zm-12 7 12 7 12-7M16 18v14M10 8l12 7" stroke="currentColor" strokeWidth="2" strokeLinejoin="round"/></svg>
        </span>
        <strong>Đúng hàng. Đúng vị trí.</strong>
        <p>Mỗi serial đều được theo dõi.</p>
        <span className="stock-check">✓</span>
      </div>
    </div>
  );
}
