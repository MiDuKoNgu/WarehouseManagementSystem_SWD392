type ParsedValue = { value: unknown; text: string; valid: boolean; absent: boolean };
export function parseAuditValue(raw: string | null): ParsedValue {
  if (!raw?.trim()) return { value: null, text: 'Không có dữ liệu.', valid: true, absent: true };
  try { const value: unknown = JSON.parse(raw); return { value, text: JSON.stringify(value, null, 2), valid: true, absent: false }; }
  catch { return { value: raw, text: raw, valid: false, absent: false }; }
}
function fields(value: unknown, prefix = '', output = new Map<string, string>(), depth = 0): Map<string, string> {
  if (output.size >= 200) return output;
  if (value !== null && typeof value === 'object' && depth < 6 && Object.keys(value).length > 0) {
    for (const [key, child] of Object.entries(value)) fields(child, prefix ? `${prefix}.${key}` : key, output, depth + 1);
  } else if (prefix) output.set(prefix, JSON.stringify(value) ?? String(value));
  return output;
}

export function AuditValues({ oldValues, newValues }: { oldValues: string | null; newValues: string | null }) {
  const before = parseAuditValue(oldValues);
  const after = parseAuditValue(newValues);
  const oldFields = before.absent ? new Map<string, string>() : fields(before.value);
  const newFields = after.absent ? new Map<string, string>() : fields(after.value);
  const changed = [...new Set([...oldFields.keys(), ...newFields.keys()])].filter(key => oldFields.get(key) !== newFields.get(key));
  return <>
    <section className="audit-changes" aria-labelledby="audit-changes-title"><h2 id="audit-changes-title">Nội dung thay đổi</h2>
      {before.valid && after.valid ? changed.length > 0 ? <div className="audit-card audit-table-wrap"><table className="audit-table audit-diff-table"><thead><tr><th>Trường dữ liệu</th><th>Trước thao tác</th><th>Sau thao tác</th></tr></thead><tbody>{changed.map(key => <tr key={key}><td><code>{key}</code></td><td className="audit-before">{oldFields.get(key) ?? 'Chưa có'}</td><td className="audit-after">{newFields.get(key) ?? 'Đã bỏ'}</td></tr>)}</tbody></table></div> : <p className="audit-muted">{before.absent && after.absent ? 'Bản ghi này không lưu dữ liệu trước/sau.' : 'Không có trường thay đổi để so sánh. Xem dữ liệu gốc bên dưới.'}</p>
        : <p className="audit-muted">Dữ liệu cũ không ở định dạng JSON hợp lệ. Nội dung được hiển thị dạng văn bản bên dưới.</p>}
      {(oldFields.size >= 200 || newFields.size >= 200) && <p className="audit-footnote">Bảng so sánh hiển thị tối đa 200 trường. Xem dữ liệu đầy đủ bên dưới.</p>}
    </section>
    <section className="audit-values" aria-label="Dữ liệu trước và sau"><article className="audit-card"><h3>Trước thao tác</h3><pre>{before.text}</pre></article><article className="audit-card"><h3>Sau thao tác</h3><pre>{after.text}</pre></article></section>
  </>;
}
