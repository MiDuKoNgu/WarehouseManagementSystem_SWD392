import { copyFile, mkdir } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const target = fileURLToPath(new URL('../public/ocr/', import.meta.url));
await mkdir(target, { recursive: true });
const packages = Object.fromEntries(['tesseract.js', 'tesseract.js-core', '@tesseract.js-data/eng']
  .map(name => [name, dirname(require.resolve(`${name}/package.json`))]));
const files = [
  ['tesseract.js', 'dist/worker.min.js', 'worker.min.js'],
  ['tesseract.js', 'LICENSE.md', 'tesseract-license.md'],
  ['tesseract.js-core', 'LICENSE', 'core-license.txt'],
  ...['', '-simd', '-relaxedsimd'].map(suffix => ['tesseract.js-core', `tesseract-core${suffix}-lstm.wasm.js`, `tesseract-core${suffix}-lstm.wasm.js`]),
  ['@tesseract.js-data/eng', '4.0.0_best_int/eng.traineddata.gz', 'eng.traineddata.gz'],
];
for (const [name, source, destination] of files) await copyFile(join(packages[name], source), join(target, destination));
console.log('Prepared local OCR worker, WASM cores and English model. No runtime CDN required.');
