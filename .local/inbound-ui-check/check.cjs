const { chromium } = require('playwright-core');
const fs = require('fs');
(async () => {
const source = fs.readFileSync('frontend/tests/inbound.test.tsx','utf8');
const dataLines = source.split('\n').filter(l => l.startsWith('const user =') || l.startsWith('const order =') || l.startsWith('const original:'));
const fixture = new Function(dataLines.join('\n').replace('const original: ReceiptDetail =','const original =') + ';return {user,order,original};')();
const { user, order, original } = fixture;
original.items = [{ inboundItemId:20, poLineId:11, scannedSerial:'Daikin-AbC-001', manufacturingMonth:8, manufacturingYear:2026, receivedCondition:'GOOD', conditionNote:null, inputMethod:'CAMERA', scannedByUserId:3, scannedByName:'Nhân viên Kho', scannedAt:'2026-11-01T03:00:00Z', serialItemId:null }];
original.receipt.serialCount=1; original.lines[0].currentQuantity=1; original.lines[0].remainingQuantity=2;
const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
try {
const page=await browser.newPage(); const errors=[];
page.on('pageerror',e=>errors.push(String(e)));
await page.route(/^http:\/\/127\.0\.0\.1:5188\/api\//,async route=>{
 const path=new URL(route.request().url()).pathname;
 let value= path==='/api/auth/me'?user:path==='/api/auth/csrf'?{token:'test-csrf'}:path==='/api/inbounds/1'?original:path==='/api/inbounds/orders'?[order]:path==='/api/inbounds/orders/10'?{order,lines:original.lines,policy:original.policy}:path==='/api/inbounds'?[{receipt:original.receipt,poCode:order.poCode,supplierName:order.supplierName,warehouseName:order.warehouseName,expectedQuantity:5}]:path==='/api/inventory/pending-putaway'?[{serialItemId:99,serialNumber:'PENDING-99',productId:1,inboundId:1,receiptCode:'INB-001',manufacturingMonth:8,manufacturingYear:2026,itemCondition:'GOOD',isQuarantined:false,registeredAt:'2026-11-01T04:00:00Z'}]:{};
 await route.fulfill({contentType:'application/json',body:JSON.stringify(value)});
});
for(const size of [{name:'desktop',width:1440,height:1100},{name:'mobile',width:390,height:844}]) {
 await page.setViewportSize(size);
 for(const [name,path,title] of [['detail','/warehouse/inbounds/1','INB-001'],['list','/warehouse/inbounds','Phiếu nhập kho'],['create','/warehouse/inbounds/new','Tạo phiếu nhập'],['pending','/warehouse/pending-putaway','Thiết bị chờ xếp kho']]) {
  await page.goto('http://127.0.0.1:5188'+path); try { await page.getByRole('heading',{name:title,exact:true}).waitFor({timeout:10000}); } catch(e) { console.log(await page.locator('body').innerText()); console.log(errors); await page.screenshot({path:'.local/inbound-ui-check/failure.png'}); throw e; }
  await page.screenshot({path:'.local/inbound-ui-check/'+size.name+'-'+name+'.png',fullPage:true});
  const dimensions=await page.evaluate(()=>({scroll:document.documentElement.scrollWidth,width:window.innerWidth}));
  if(dimensions.scroll>dimensions.width) throw new Error(size.name+' '+name+' horizontal overflow '+JSON.stringify(dimensions));
 }
}
if(errors.length)throw new Error(errors.join('\n'));
console.log('PASS: 8 Chrome viewport/page combinations, no horizontal overflow or page errors. Screenshots in .local/inbound-ui-check. Fixture responses used for visual QA only.');
} finally {await browser.close();}
})();
