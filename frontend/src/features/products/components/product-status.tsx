export function ProductStatus({ status }: { status: string }) {
  return <span className={`product-status ${status === 'ACTIVE' ? 'is-active' : ''}`}><span />{status === 'ACTIVE' ? 'Đang hoạt động' : 'Ngừng hoạt động'}</span>;
}
