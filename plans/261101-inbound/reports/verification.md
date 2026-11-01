# Inbound — Manifest và kiểm chứng

Ngày kiểm chứng: 01/11/2026 (Asia/Saigon). Phạm vi: tiếp nhận theo Serial từ PO, backend/frontend, audit và hàng chờ Putaway. Không triển khai cấp vị trí Putaway.

## File chính theo module

| Module | File/thư mục | Thay đổi |
|---|---|---|
| Inventory contracts/rules | `Inventory.Models/Inbound/InboundContracts.cs`, `Inventory.BLL/Inbound/InboundRules.cs` | DTO, RowVersion, Serial/tháng năm/tình trạng/phương thức |
| Inventory persistence | `Inventory.DAL/Inbound/SqlInboundRepository.cs` | Transaction nhận hàng, item lifecycle, idempotency, abort fence, audit |
| Inventory read models | `Inventory.DAL/Receiving/ReceiptQueries.cs`, `ReceivingSummaryRepository.cs` | Pending Serial, reservation proof, tương thích DONE/COMPLETED |
| Inventory HTTP | `Inventory.API/Controllers/InboundsController.cs`, `Clients/ReceivingOrderClient.cs`, `Program.cs` | Kho/role/CSRF, orchestration nhận PO và retry terminal |
| Procurement coordination | `Procurement.DAL/Receiving/SqlReceivingReservationRepository.cs`, `Procurement.API/Controllers/ReceivingOrdersController.cs` | Durable reserve/release dưới khóa PO |
| Procurement guards/contracts | `Procurement.DAL/PurchaseOrders/SqlPurchaseOrderRepository.cs`, `Procurement.Models/Receiving/`, `Procurement.API/Program.cs` | Chặn close/cancel race và đăng ký service |
| Lookup authorization | Catalog/Warehouse options controllers | Warehouse Staff đọc metadata phục vụ nhận PO, không cấp mutation PO |
| Frontend | `frontend/src/features/inbound/` | List/create/detail, edit/remove/complete, camera/ảnh/OCR, pending list, đồng bộ PO |
| Navigation/config | `frontend/src/app/app.tsx`, dashboard workspace, `frontend/vite.config.ts`, package files | Route Warehouse và proxy/dependencies scanner/OCR |
| Migration | `scripts/sql/migrate-inbound.sql`, `migrate-procurement-receiving.sql`, `scripts/migrate-inbound.ps1` | DDL bổ sung, UNKNOWN lịch sử, reconciliation kho theo PO |
| Tests | `tests/Inventory.Inbound.Tests/`, `tests/Procurement.Tests/`, frontend Inbound tests | Rule/API/SQL/concurrency/regression |
| Docs | `docs/inbound.md`, `docs/journals/261101-inbound.md` | Cách dùng, API, migration, quyết định và giới hạn |

## Kết quả đã chạy

| Kiểm tra | Kết quả |
|---|---|
| Solution build | PASS, 0 warnings, 0 errors |
| Inbound rules/API/service client | PASS, kiểm tra role, warehouse, CSRF và forwarding/errors |
| Inbound real SQL integration | PASS trên database tạm tại MIDUKONGU; chạy migration hai lần, nhiều đợt, duplicate đồng thời, quantity reservations, DONE bất biến, rollback, quarantine, pending stock, audit và abort fence |
| Procurement API/SQL | PASS |
| Identity, PO Excel, audit suites | PASS |
| Frontend | PASS 41/41 tests, TypeScript và production build PASS |
| Chrome desktop/mobile | 8 ảnh kiểm tra trên Chrome thật với API mock, không tràn ngang/lỗi JavaScript |
| Migration local | PASS; cả hai script đã áp dụng qua `scripts/migrate-inbound.ps1`; không có dòng reconciliation được báo ra |
| Catalog.Products.Tests | Chưa compile: ProductTests.cs:65 tham chiếu SqlProductTests, thiếu file/class từ source ban đầu |
| Điện thoại thật qua HTTPS | Chưa kiểm tra trực tiếp |

## Giới hạn và phục hồi

Chính sách đã được người dùng xác nhận: một DRAFT mỗi PO; hàng hỏng nặng từ chối; hỏng nhẹ/móp méo chấp nhận dưới mã DAMAGED và cách ly; nhân viên được hủy DRAFT có lý do. Cấu hình AllowMultipleDrafts=false, AcceptDamaged=true, AllowCancellation=true. DAMAGED bắt buộc ghi chú, nguồn hàng EligibleQuantity=0. UI hỏng nặng không lưu Serial; backend từ chối trạng thái ngoài GOOD/DAMAGED. Nhân viên phân loại mức độ, không bổ sung severity schema. New writes chỉ nhận MANUAL/CAMERA/IMAGE_UPLOAD; UNKNOWN bảo toàn sự thiếu metadata lịch sử. Serial mới chờ Putaway dùng IN_WAREHOUSE với NULL location, đúng schema hiện có.

Reservation không tự hết hạn khi kết quả tạo chưa rõ. Client giữ token retry; nếu local client storage mất cần operator đối chiếu trạng thái Inventory trước khi can thiệp. Không giải phóng reservation chỉ vì timeout. DONE/CANCELLED có nút **Đồng bộ trạng thái PO** để retry release an toàn. Failed-create tombstone CANCELLED được audit, không hủy DRAFT đang tồn tại.

Camera/OCR đã có xử lý quyền và nhận diện nhưng unit test không chứng minh chất lượng trên camera/tem thực. Sau bản sửa camera/OCR, tài nguyên OCR được phục vụ cùng website từ /ocr/, không cần CDN runtime. Xem serial-recognition-fix.md. Không tuyên bố toàn bộ regression suites pass khi Catalog test source còn thiếu. Không tạo commit; workspace không có .git và người dùng chưa yêu cầu commit.


