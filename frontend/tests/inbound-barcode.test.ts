import { expect, it } from 'vitest';
import { BarcodeFormat, BinaryBitmap, DecodeHintType, HybridBinarizer, MultiFormatReader, RGBLuminanceSource } from '@zxing/library';
import { barcodeHints } from '../src/features/inbound/barcode-hints';

// Independent EAN-13 fixture with quiet zones and a known checksum, rendered as luminance pixels.
function eanImage(digits: string) {
  const left = ['0001101','0011001','0010011','0111101','0100011','0110001','0101111','0111011','0110111','0001011'];
  const invert = (bits: string) => bits.replace(/[01]/g, b => b === '0' ? '1' : '0');
  const parity = ['LLLLLL','LLGLGG','LLGGLG','LLGGGL','LGLLGG','LGGLLG','LGGGLL','LGLGLG','LGLGGL','LGGLGL'][Number(digits[0])];
  let bits = '000000000000101';
  for (let i = 1; i <= 6; i++) bits += parity[i - 1] === 'L' ? left[Number(digits[i])] : invert(left[Number(digits[i])]).split('').reverse().join('');
  bits += '01010';
  for (let i = 7; i < 13; i++) bits += invert(left[Number(digits[i])]);
  bits += '101000000000000';
  const width = bits.length * 4; const height = 160; const pixels = new Uint8ClampedArray(width * height);
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) pixels[y * width + x] = bits[Math.floor(x / 4)] === '1' ? 0 : 255;
  return new BinaryBitmap(new HybridBinarizer(new RGBLuminanceSource(pixels, width, height)));
}
it('decodes the actual user numeric barcode using the real ZXing decoder and serial-mode formats', () => {
  const result = new MultiFormatReader().decode(eanImage('8934804025742'), barcodeHints());
  expect(result.getText()).toBe('8934804025742');
  expect(result.getBarcodeFormat()).toBe(BarcodeFormat.EAN_13);
});
it('rejects an incorrect EAN checksum rather than accepting partial digits', () => {
  expect(() => new MultiFormatReader().decode(eanImage('8934804025743'), barcodeHints())).toThrow();
});
it('keeps ambiguous ITF/Codabar formats opt-in while retaining EAN/UPC support', () => {
  const formats = barcodeHints().get(DecodeHintType.POSSIBLE_FORMATS) as BarcodeFormat[];
  expect(formats).toContain(BarcodeFormat.EAN_13);
  expect(formats).not.toContain(BarcodeFormat.ITF);
  expect(formats).not.toContain(BarcodeFormat.CODABAR);
  expect(barcodeHints(true).has(DecodeHintType.POSSIBLE_FORMATS)).toBe(false);
});
