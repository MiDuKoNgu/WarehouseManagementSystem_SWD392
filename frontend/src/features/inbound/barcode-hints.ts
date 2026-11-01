import { BarcodeFormat, DecodeHintType } from '@zxing/library';

export function barcodeHints(includeAll = false) {
  const hints = new Map<DecodeHintType, unknown>();
  hints.set(DecodeHintType.TRY_HARDER, true);
  // ITF/Codabar can falsely decode fragments of a longer label into short numbers.
  // Keep them opt-in; EAN/UPC remain supported and checked by the decoder.
  if (!includeAll) hints.set(DecodeHintType.POSSIBLE_FORMATS, [
    BarcodeFormat.CODE_128, BarcodeFormat.CODE_39, BarcodeFormat.CODE_93,
    BarcodeFormat.QR_CODE, BarcodeFormat.DATA_MATRIX, BarcodeFormat.PDF_417, BarcodeFormat.AZTEC,
    BarcodeFormat.EAN_13, BarcodeFormat.EAN_8, BarcodeFormat.UPC_A, BarcodeFormat.UPC_E,
  ]);
  return hints;
}
