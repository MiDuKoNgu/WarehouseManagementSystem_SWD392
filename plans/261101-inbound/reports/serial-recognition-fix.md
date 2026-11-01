# Camera and uploaded Serial recognition fix

User reported camera output 668152 while testing 8934804025742 and OCR selecting £ from `Serial £` above another line of serial text. Future device is iPhone; previous test Android.

Root causes: parser captured arbitrary same-line suffix and ignored the next line; camera accepted any one decode and scanned unrelated codes across the full frame; default all-format decoding included ambiguous ITF/Codabar; OCR worker/model fetched third-party CDN at runtime (actual browser request failed with ERR_NETWORK_ACCESS_DENIED).

Changes: multiline labelled candidate extraction with noise filtering and case preservation; three consistent reads with timeout and explicit rescan latch; actual centre crop matched to video overlay; high-resolution rear-camera constraints; ITF/Codabar opt-in with EAN/UPC retained. Uploaded images use overlapping crops and rotation retries, small-image scaling and sparse/block OCR layouts. Local same-origin worker/three compatible WASM variants/English model prepared automatically before dev/build, licenses included. No backend, database or receipt workflow changes.

Validation: full frontend suite 48 tests pass, production build passes. Real ZXing independently generated EAN fixture decodes 8934804025742; wrong checksum rejected. Original OCR text regression returns 2691A27533437L0, never £. Real Chrome at 390px viewport: actual PNG upload -> barcode+Tesseract result -> candidate selection -> exact form value. Canvas video stream -> actual camera decoder -> centre crop -> three-read consensus -> choose -> exact form value; no page errors. Browser tests use synthetic label/stream and fake API reads, do not write business data. No physical iPhone test or access to the original label photo. Images under .local/inbound-ui-check/.

Retest: restart frontend `npm.cmd run dev`, retain backend/tunnel, refresh HTTPS tab on iPhone Safari. Place one full barcode with quiet margins inside guide; wait stable result, compare and select. Use clear JPEG/PNG/WebP for upload.

Library API references: [Tesseract parameters](https://github.com/naptha/tesseract.js/blob/master/docs/api.md), [local OCR assets](https://github.com/naptha/tesseract.js/blob/master/docs/local-installation.md). Installed package APIs verified against node_modules types/source.
