/** Only explicitly labelled OCR serials are candidates. Model/SKU/EAN text is never silently selected. */
export function serialCandidates(text: string): string[] {
  const candidates = new Set<string>();
  const lines = text.split(/\r?\n/);
  const label = /(?:^|\b)(?:serial(?:[ \t]+(?:number|no\.?))?|s[ \t]*\/[ \t]*n|s\.?n\.?)(?=$|[ \t:#=\-])/i;
  const metadata = /^(?:model|sku|ean|upc|part|p\/n|date|voltage|made|manufactur)\b/i;
  function candidate(line: string) {
    // Remove border/punctuation noise, never substitute O/0, I/1 or alter case inside a serial.
    const clean = line.trim().replace(/^[\s:#=|]+/, '').replace(/[\s|,;]+[-|\s]*$/, '');
    const token = clean.split(/\s/)[0];
    return token && token.length >= 3 && token.length <= 100 && /^[A-Za-z0-9](?:[A-Za-z0-9._/:-]*[A-Za-z0-9])$/.test(token)
      && !/^(?:number|serial|model|sku|ean|upc)$/i.test(token) ? token : null;
  }
  for (let i = 0; i < lines.length; i++) {
    const match = label.exec(lines[i]);
    if (!match) continue;
    const inline = candidate(lines[i].slice(match.index + match[0].length));
    if (inline) { candidates.add(inline); continue; }
    // Label and value often occupy separate lines; ignore isolated OCR noise such as £.
    for (let next = i + 1; next < Math.min(lines.length, i + 3); next++) {
      if (metadata.test(lines[next].trim()) || label.test(lines[next])) break;
      const value = candidate(lines[next]);
      if (value) { candidates.add(value); break; }
    }
  }
  return [...candidates];
}
export function scanConsensus() {
  let last = ''; let count = 0; let at = 0; let locked = false;
  return {
    reset() { last = ''; count = 0; at = 0; locked = false; },
    read(raw: string, now = Date.now()) {
      const value = raw.trim();
      if (locked || !value || value.length > 100 || /[\x00-\x1f]/.test(value)) return null;
      count = value === last && now - at <= 1500 ? count + 1 : 1;
      last = value; at = now;
      if (count < 3) return null;
      locked = true; return value;
    },
  };
}
export type ImageRecognition = { barcodes: string[]; ocr: string[]; text: string; warnings: string[] };
export function singleImageSerial(result: ImageRecognition): string | null {
  const values = [...new Set([...result.barcodes, ...result.ocr])];
  return values.length === 1 ? values[0] : null;
}
export async function validateLabelImage(file: File): Promise<void> {
  if (file.size === 0 || file.size > 10 * 1024 * 1024) throw new Error('Ảnh cần có dung lượng từ 1 byte đến 10 MB.');
  if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) throw new Error('Chỉ hỗ trợ ảnh JPEG, PNG hoặc WebP.');
  const b = new Uint8Array(await file.slice(0, 12).arrayBuffer());
  const jpeg = b[0] === 0xff && b[1] === 0xd8 && b[2] === 0xff;
  const png = [137, 80, 78, 71, 13, 10, 26, 10].every((v, i) => b[i] === v);
  const webp = String.fromCharCode(...b.slice(0, 4)) === 'RIFF' && String.fromCharCode(...b.slice(8, 12)) === 'WEBP';
  if (!(file.type === 'image/jpeg' && jpeg || file.type === 'image/png' && png || file.type === 'image/webp' && webp)) throw new Error('Nội dung file không khớp định dạng ảnh.');
}
export async function recognizeLabel(file: File, signal?: AbortSignal): Promise<ImageRecognition> {
  await validateLabelImage(file);
  const { BrowserMultiFormatReader } = await import('@zxing/browser');
  const { barcodeHints } = await import('./barcode-hints');
  const url = URL.createObjectURL(file); const barcodes = new Set<string>(); const warnings: string[] = [];
  let text = '';
  try {
    const img = new Image(); img.src = url; await img.decode();
    if (img.naturalWidth * img.naturalHeight > 32_000_000) throw new Error('Ảnh quá lớn. Vui lòng giảm xuống dưới 32 megapixel.');
    if (signal?.aborted) throw new DOMException('Aborted', 'AbortError');
    const reader = new BrowserMultiFormatReader(barcodeHints());
    try { barcodes.add((await reader.decodeFromImageElement(img)).getText()); } catch { /* OCR remains available when no barcode decodes. */ }
    // Cropped regions let ZXing find other labels when an image contains several codes.
    const canvas = document.createElement('canvas'); const context = canvas.getContext('2d');
    if (context) {
      // Overlap crops to keep complete barcode + quiet zone; non-overlapping thirds cut codes apart.
      for (let y = 0; y < 2; y++) for (let x = 0; x < 2; x++) {
        if (signal?.aborted) throw new DOMException('Aborted', 'AbortError');
        const width = img.naturalWidth * .75; const height = img.naturalHeight * .75;
        const scale = Math.min(2, 2400 / Math.max(width, height));
        canvas.width = Math.round(width * scale); canvas.height = Math.round(height * scale);
        context.drawImage(img, x * img.naturalWidth * .25, y * img.naturalHeight * .25, width, height, 0, 0, canvas.width, canvas.height);
        try { barcodes.add(reader.decodeFromCanvas(canvas).getText()); } catch { /* This region can have no readable code. */ }
      }
      if (!barcodes.size) {
        for (const angle of [0, Math.PI / 2, Math.PI, -Math.PI / 2]) {
          if (signal?.aborted) throw new DOMException('Aborted', 'AbortError');
          const sideways = Math.abs(angle) === Math.PI / 2;
          const scale = Math.min(2, 2400 / Math.max(img.naturalWidth, img.naturalHeight));
          canvas.width = Math.round((sideways ? img.naturalHeight : img.naturalWidth) * scale);
          canvas.height = Math.round((sideways ? img.naturalWidth : img.naturalHeight) * scale);
          context.save(); context.translate(canvas.width / 2, canvas.height / 2); context.rotate(angle);
          context.drawImage(img, -img.naturalWidth * scale / 2, -img.naturalHeight * scale / 2, img.naturalWidth * scale, img.naturalHeight * scale); context.restore();
          try { barcodes.add(reader.decodeFromCanvas(canvas).getText()); break; } catch { /* Try the next orientation. */ }
        }
      }
    }
    if (signal?.aborted) throw new DOMException('Aborted', 'AbortError');
    try {
      const { createWorker, PSM } = await import('tesseract.js');
      const localAssets = new URL(`${import.meta.env.BASE_URL}ocr/`, window.location.origin).href;
      const worker = await createWorker('eng', 1, {
        workerPath: `${localAssets}worker.min.js`, corePath: localAssets,
        langPath: localAssets, workerBlobURL: false,
      });
      const abort = () => { void worker.terminate(); };
      signal?.addEventListener('abort', abort, { once: true });
      try {
        if (signal?.aborted) throw new DOMException('Aborted', 'AbortError');
        const scale = Math.min(2, 2400 / Math.max(img.naturalWidth, img.naturalHeight));
        const ocrCanvas = document.createElement('canvas');
        ocrCanvas.width = Math.round(img.naturalWidth * scale); ocrCanvas.height = Math.round(img.naturalHeight * scale);
        const ocrContext = ocrCanvas.getContext('2d');
        if (ocrContext) { ocrContext.fillStyle = '#fff'; ocrContext.fillRect(0, 0, ocrCanvas.width, ocrCanvas.height); ocrContext.filter = 'grayscale(1) contrast(1.2)'; ocrContext.drawImage(img, 0, 0, ocrCanvas.width, ocrCanvas.height); }
        const input = ocrContext ? ocrCanvas : img;
        await worker.setParameters({ tessedit_pageseg_mode: PSM.SPARSE_TEXT, preserve_interword_spaces: '1', user_defined_dpi: '300' });
        text = (await worker.recognize(input)).data.text;
        // A second layout pass helps labels split across lines; retain both results for review.
        if (!serialCandidates(text).length) {
          await worker.setParameters({ tessedit_pageseg_mode: PSM.SINGLE_BLOCK });
          const retry = (await worker.recognize(input)).data.text;
          if (retry !== text) text += '\n' + retry;
        }
      } finally { signal?.removeEventListener('abort', abort); await worker.terminate(); }
    } catch (e) {
      if (signal?.aborted) throw e;
      warnings.push('OCR chưa xử lý được ảnh. Thử ảnh rõ hơn hoặc nhập Serial thủ công. Nếu vẫn lỗi, khởi động lại frontend để chuẩn bị bộ nhận diện.');
    }
  } finally { URL.revokeObjectURL(url); }
  const ocr = serialCandidates(text);
  const codes = [...barcodes].map(v => v.trim()).filter(v => v.length > 0 && v.length <= 100);
  if (codes.length && ocr.length && !codes.some(v => ocr.includes(v))) warnings.push('Barcode và chữ Serial không khớp. Đối chiếu tem và chọn đúng Serial trước khi thêm.');
  return { barcodes: [...new Set(codes)], ocr, text, warnings };
}
