const { chromium } = require('playwright-core');
(async () => {
  const browser = await chromium.launch({ executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    const errors = []; page.on('pageerror', e => errors.push(String(e)));
    page.on('requestfailed', request => console.log('REQUEST FAILED', request.url(), request.failure()?.errorText));
    page.on('console', message => { if (message.type() === 'error') console.log('CONSOLE', message.text()); });
    const fs = require('fs');
    const source = fs.readFileSync('frontend/tests/inbound.test.tsx', 'utf8');
    const lines = source.split('\n').filter(line => line.startsWith('const user =') || line.startsWith('const order =') || line.startsWith('const original:'));
    const fixture = new Function(lines.join('\n').replace('const original: ReceiptDetail =', 'const original =') + ';return {user,order,original};')();
    await page.route('**/api/**', route => {
      const path = new URL(route.request().url()).pathname;
      if (!path.startsWith('/api/')) return route.continue();
      const value = path === '/api/auth/me' ? fixture.user : path === '/api/inbounds/1' ? fixture.original : {};
      return route.fulfill({ contentType: 'application/json', body: JSON.stringify(value) });
    });
    await page.goto('http://[::1]:5173/warehouse/inbounds/1');
    await page.getByRole('heading', { name: 'Tiếp nhận Serial', exact: true }).waitFor();
    const result = await page.evaluate(async () => {
      const { recognizeLabel } = await import('/src/features/inbound/recognition.ts');
      const c = document.createElement('canvas'); c.width = 1000; c.height = 500;
      const ctx = c.getContext('2d'); ctx.fillStyle = '#fff'; ctx.fillRect(0, 0, c.width, c.height);
      const digits = '8934804025742';
      const left = ['0001101','0011001','0010011','0111101','0100011','0110001','0101111','0111011','0110111','0001011'];
      const invert = bits => bits.replace(/[01]/g, b => b === '0' ? '1' : '0');
      const parity = 'LGLGGL'; let bits = '101';
      for (let i = 1; i <= 6; i++) bits += parity[i - 1] === 'L' ? left[Number(digits[i])] : invert(left[Number(digits[i])]).split('').reverse().join('');
      bits += '01010'; for (let i = 7; i < 13; i++) bits += invert(left[Number(digits[i])]); bits += '101';
      ctx.fillStyle = '#000'; ctx.font = '36px Arial'; ctx.fillText('Serial Number:', 120, 65); ctx.fillText(digits, 120, 115);
      for (let i = 0; i < bits.length; i++) if (bits[i] === '1') ctx.fillRect(120 + i * 7, 160, 7, 170);
      ctx.font = '28px Arial'; ctx.fillText(digits, 120, 375);
      const image = c.toDataURL();
      const file = new File([await new Promise(resolve => c.toBlob(resolve, 'image/png'))], 'ean-serial.png', { type: 'image/png' });
      const controller = new AbortController(); const timer = setTimeout(() => controller.abort(), 45000);
      let recognition; try { recognition = await recognizeLabel(file, controller.signal); } finally { clearTimeout(timer); }
      return { image, recognition };
    });
    if (!result.recognition.barcodes.includes('8934804025742')) throw new Error('Real browser barcode decode failed: ' + JSON.stringify(result.recognition));
    console.log(JSON.stringify({ recognition: result.recognition, errors }));
    const imageBytes = Buffer.from(result.image.split(',')[1], 'base64');
    await page.locator('input[type=file]').setInputFiles({ name: 'serial-ean.png', mimeType: 'image/png', buffer: imageBytes });
    await page.waitForFunction(() => document.querySelector('.inbound-serial-form input[autocomplete="off"]')?.value === '8934804025742', null, { timeout: 45000 });
    if (await page.getByLabel('Serial', { exact: true }).inputValue() !== '8934804025742') throw new Error('Upload choice failed to fill exact serial');
    if (await page.locator('.inbound-recognition').count()) throw new Error('Automatic OCR panel should be hidden');
    await page.getByRole('button', { name: 'Quét lại', exact: true }).click();
    if (await page.getByLabel('Serial', { exact: true }).inputValue() !== '') throw new Error('Image rescan must clear Serial');
    await page.evaluate(async image => {
      const img = new Image(); img.src = image; await img.decode();
      const camera = document.createElement('canvas'); camera.width = 1000; camera.height = 500;
      camera.getContext('2d').drawImage(img, 0, 0);
      Object.defineProperty(navigator.mediaDevices, 'getUserMedia', { configurable: true, value: async () => {
        const stream = camera.captureStream(10);
        setInterval(() => camera.getContext('2d').drawImage(img, 0, 0), 100);
        return stream;
      } });
    }, result.image);
    await page.getByLabel('Serial', { exact: true }).fill('');
    await page.getByRole('button', { name: 'Quét bằng camera', exact: true }).click();
    try { await page.locator('.inbound-camera .inbound-serial').waitFor({ timeout: 12000 }); }
    catch (error) {
      console.log(await page.locator('.serial-capture').innerText());
      console.log(await page.locator('video').evaluate(v => ({width:v.videoWidth,height:v.videoHeight,paused:v.paused,state:v.readyState,time:v.currentTime,tracks:v.srcObject?.getTracks().map(t=>({state:t.readyState,enabled:t.enabled}))})));
      await page.screenshot({path:'.local/inbound-ui-check/camera-regression-failure.png',fullPage:true}); throw error;
    }
    if (await page.locator('.inbound-camera .inbound-serial').innerText() !== '8934804025742') throw new Error('Real camera decoder/centre crop/consensus returned wrong digits');
    if (await page.getByRole('button', { name: 'Dùng mã camera này', exact: true }).count()) throw new Error('Camera must autofill without selection button');
    if (await page.getByLabel('Serial', { exact: true }).inputValue() !== '8934804025742') throw new Error('Camera choice failed');
    if (await page.locator('.inbound-camera select').count()) throw new Error('Format picker must be hidden');
    await page.locator('.inbound-camera').screenshot({path:'.local/inbound-ui-check/camera-regression.png'});
    await page.getByRole('button', { name: 'Quét lại', exact: true }).click();
    if (await page.getByLabel('Serial', { exact: true }).inputValue() !== '') throw new Error('Rescan did not clear Serial form field');
    await page.getByRole('button', { name: 'Đóng camera', exact: true }).click();
    await page.getByLabel('Serial', { exact: true }).scrollIntoViewIfNeeded();
    await page.screenshot({ path: '.local/inbound-ui-check/recognition-regression.png', fullPage: true });
    if (!result.recognition.ocr.includes('8934804025742')) throw new Error('Real OCR did not confirm generated label (see warnings above).');
    if (errors.length) throw new Error(errors.join('\n'));
    console.log('PASS real browser file upload + Tesseract OCR + camera stream with real decoder, centre crop and three-read consensus. Synthetic label/stream, not a physical phone test.');
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
