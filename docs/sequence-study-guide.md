# Hướng dẫn 12 sequence diagrams và phân công nhóm

## Bộ tài liệu

- [12 sequence diagrams có activation bar](wms-sequence-study-12.drawio): file mới, 12 trang SD01–SD12.
- [Class diagram model gốc](wms-domain-class-diagrams.drawio): 5 trang, dùng làm CD01–CD05.
- [Class Specifications](model-class-specifications.md): 16 model, đặc tả một lần rồi dẫn lại.
- Các file sequence cũ vẫn là bản tổng quan dài; dùng file 12 trang này để chia việc và trình bày báo cáo.

## Cách hiểu yêu cầu của thầy

> For the features/functions with the same structure of class & sequence diagrams, you need to provide the diagrams once for one feature/function and refer to those diagrams from other features/functions.

Áp dụng: mỗi nhóm model có một class diagram chuẩn. Mỗi cấu trúc xử lý trùng nhau có một sequence đại diện hoặc một interaction con được đặt tên. Chức năng khác chỉ ghi tham số/điều kiện thay đổi và dùng `ref` trỏ đúng tương tác đó. Không gom các nghiệp vụ khác biệt chỉ vì chúng cùng dùng một controller.

**Lưu ý số lượng:** 5 người × 2 sequence = 10. Để có đúng 12 mà vẫn phân mỗi người 2 sơ đồ nghiệp vụ, bộ này có **10 sơ đồ nghiệp vụ + 2 sơ đồ dùng chung**. SD01–SD02 do nhóm cùng rà soát, có thể cử trưởng nhóm quản lý. Đây không phải 12 sơ đồ mỗi người đều nhận đúng 2; nếu thầy muốn cả 12 được giao cá nhân, hai người phải nhận thêm 1 sơ đồ.

## Phân công đề xuất

| Người | Sequence thứ nhất | Sequence thứ hai | Class diagram/spec dùng lại |
|---|---|---|---|
| Người 1 | SD03 — Login | SD04 — Tạo/sửa PO | CD01 + CD02 |
| Người 2 | SD05 — Import Excel PO | SD06 — Confirm/cancel/close PO | CD02 từ Người 1 |
| Người 3 | SD07 — Tạo Inbound | SD08 — Thêm/sửa/xóa serial | CD03 |
| Người 4 | SD09 — Complete/cancel Inbound | SD10 — Tra cứu/gợi ý Putaway | CD03 + CD04 |
| Người 5 | SD11 — Confirm Putaway | SD12 — Xem Audit Log | CD04 + CD05 |
| Dùng chung | SD01 — Cấp CSRF token | SD02 — Kiểm phiên/quyền/CSRF | Ref từ các sơ đồ nghiệp vụ |

Tên Người 1…5 là vị trí phân công để nhóm thay bằng tên thật. Số lượng bằng nhau nhưng độ khó khác nhau; nên cùng rà soát SD07/SD09 vì có tương tác liên service và retry.

## Class diagram và Class Specifications theo feature

| Feature | Class diagram đặt một lần | Specifications cần đọc | Sequence |
|---|---|---|---|
| Authentication | CD01 = trang 01 | CS03 User, CS02 Role, CS01 Warehouse | SD03; ref SD01/SD02 nếu thích hợp |
| Purchase Order | CD02 = trang 02 | CS05 PurchaseOrder, CS06 PurchaseOrderLine, CS04 Supplier, CS07 Product, CS03 User, CS01 Warehouse | SD04–SD06 |
| Inbound | CD03 = trang 03 | CS08 Inbound, CS09 InboundItem, CS10 SerialItem, CS05 PurchaseOrder, CS06 PurchaseOrderLine, CS03 User, CS01 Warehouse | SD07–SD09 |
| Putaway | CD04 = trang 04 | CS10 SerialItem, CS12 StorageLocation, CS11 SerialMovement, CS07 Product, CS01 Warehouse, CS13 ZoneStorageRule, CS14 Category, CS15 Brand, CS03 User | SD10–SD11 |
| Xem Audit Log | CD05 = trang 05 | CS16 AuditLog, CS03 User, CS02 Role | SD12 |

Model gốc mô tả dữ liệu; lifeline sequence mô tả đối tượng xử lý thật (controller/service/repository). Ví dụ `PurchaseOrderService.ConfirmOrderAsync` không có nghĩa class model `PurchaseOrder` hiện có hàm `confirm()`. Không tự chép hàm service vào model.

## Cách đọc activation bar, ref và fragment

- Thanh dọc mảnh trên lifeline là **activation bar**: đối tượng đang xử lý hoặc chờ lời gọi con kết thúc. Nó không phải vòng đời đối tượng và không đồng nghĩa transaction đang mở.
- Mũi tên liền gọi xử lý; mũi tên nét đứt trả kết quả. Self-call có activation lồng ngắn.
- `break [điều kiện]`: nếu đúng thì trả lỗi và chấm dứt tương tác; không chạy các bước thành công phía dưới. Thanh activation xuyên vùng break thể hiện đường đi thành công khi điều kiện lỗi không xảy ra.
- `opt`: đoạn chỉ chạy khi điều kiện đúng. `alt/else`: các lựa chọn loại trừ nhau. `par/and`: các nhánh đồng thời; trong sơ đồ nguồn dùng chung có thể biểu diễn một cấu trúc đại diện cho hai instance.
- Khung `ref SDxx` gọi/tái sử dụng toàn sơ đồ; `ref SDxx.FRAGMENT` chỉ dùng interaction con được đặt tên. Các khung ref có liên kết tới trang đích trong file draw.io; tìm đúng tên fragment trên trang.
- Một ref giữ nguyên vai trò caller được ánh xạ: ví dụ `SD04.OPTIONS` luôn do PurchaseOrderService gọi lookup; `SD07.ORDER` do InboundsController gọi ReceivingOrderClient; `SD12.SOURCE_READ` do AuditGateway gọi đúng service nguồn.
- Một vài lifeline gộp API/DAL nguồn phụ để dễ đọc; tên repository được ghi ngay trong message. Database không bị bỏ qua trong tương tác đọc/ghi chính.
- Ref là tái sử dụng mô tả thiết kế; không đồng nghĩa code có một hàm tên “SDxx”.

## Hợp đồng các tương tác dùng lại

| Ref | Đầu vào / caller | Đầu ra | Ghi chú |
|---|---|---|---|
| SD01 | Frontend, cookie hiện tại nếu có | CSRF token + cookie khi cần | Không truy vấn DB; dùng trước mutation |
| SD02 | Service nhận request, requiredRole, requireCsrf | Cho chạy action hoặc HTTP lỗi | Internal call thẳng Identity |
| SD02.SESSION | Identity Cookie Auth + principal | Principal hợp lệ hoặc bị reject | Chỉ kiểm cookie/user, không gọi lại cả SD02 |
| SD04.OPTIONS | PurchaseOrderService | PurchaseOrderOptions | Catalog + Warehouse gọi song song |
| SD04.WRITE_CREATE | request, actor, action, importKey? | StoredPurchaseOrder | CREATE_PO hoặc IMPORT_PO_EXCEL; có importKey thì chống trùng |
| SD06.RECEIVING | PurchaseOrderService + poId | ReceivingSummary | Service gọi ValidateReceiving sau khi nhận |
| SD07.ORDER | InboundsController + poId | ReceiptOrder đúng kho | Lookup tên từ Catalog/Warehouse |
| SD07.LOAD | InboundsController + inboundId/actor | ReceiptDetail | Header + ORDER + progress/items + policy |
| SD09.RELEASE | InboundsController + inboundId/poId | Reservation được RELEASED hoặc lỗi | Chỉ sau proof terminal; không rollback Inbound đã commit |
| SD10.READ_CONTEXT | PutawayRepository, actor/id, locked, transaction? | Serial + warehouse snapshot | Occupants được đọc riêng; locked=true cho SD11 |
| SD12.SOURCE_READ | AuditGateway, source, mode=page/detail/actions | Page/detail/actions | Inventory và Procurement dùng cùng cấu trúc |
| SD12.EMPLOYEES | AuditGateway + cookie | AuditEmployee[] | Identity kiểm ADMIN |

SD04 → SD06.RECEIVING và SD06 → SD04.OPTIONS **không phải vòng lặp chạy**: chúng tham chiếu các interaction con khác nhau. Tương tự SD07.LOAD → SD07.ORDER và SD12 → SD12.SOURCE_READ không gọi đệ quy toàn bộ diagram.

## Đặc tả từng sequence và luồng đọc code

### SD01 — Cấp CSRF token

**Phân công:** Dùng chung • không yêu cầu đã đăng nhập. **Feature:** Dùng chung. **Class diagram:** Tham chiếu model Authentication khi cần; không thêm class Token nghiệp vụ..

**Tiền điều kiện:** Caller chuẩn bị gửi mutation; có hoặc không có phiên đăng nhập.

**Hậu điều kiện:** Nhận CSRF request token; trình duyệt giữ cookie CSRF khi cần.

**Lifeline:** Frontend / request / authApi → WMS.API / ApiServiceProxy → Identity.API / AuthController → ASP.NET Core / IAntiforgery. Đây là danh sách thành phần, không khẳng định mọi lời gọi đi tuyến tính theo danh sách.

**Luồng chính và điểm tái sử dụng:**

1. `f → g`: GET /api/auth/csrf.
2. `g → c`: Proxy tới Identity.API.
3. `c → a`: GetAndStoreTokens(HttpContext).

Ký hiệu lifeline: `f` = Frontend / request / authApi; `g` = WMS.API / ApiServiceProxy; `c` = Identity.API / AuthController; `a` = ASP.NET Core / IAntiforgery.

**Ghi chú nghiệp vụ:**

- Hậu điều kiện: frontend có token cho X-CSRF-TOKEN. Endpoint có [SkipUserLookup], không truy vấn SQL. Nếu có cookie phiên, antiforgery vẫn gắn token với danh tính tương ứng. Lỗi HTTP → caller dừng mutation.

**Nguồn code:** [frontend/src/features/auth/api/auth-api.ts](../frontend/src/features/auth/api/auth-api.ts), [Identity.API/Controllers/AuthController.cs](../Identity.API/Controllers/AuthController.cs).

### SD02 — Kiểm tra phiên, quyền và CSRF

**Phân công:** Dùng chung • tham số requiredRole, requireCsrf. **Feature:** Dùng chung. **Class diagram:** CD01 — Authentication.

**Tiền điều kiện:** Request tới endpoint được bảo vệ; caller truyền requiredRole và requireCsrf.

**Hậu điều kiện:** Cho phép action chạy hoặc kết thúc bằng 400/401/403/503.

**Lifeline:** Service nhận request / Middleware / filter → IdentitySessionHandler / SessionCsrfFilter → Identity.API / AuthController / Cookie Auth → SessionValidation → UserRepository → IdentityDB / User + Role. Đây là danh sách thành phần, không khẳng định mọi lời gọi đi tuyến tính theo danh sách.

**Luồng chính và điểm tái sử dụng:**

1. `c → h`: HandleAuthenticateAsync().
2. **Nhánh lỗi — Không có cookie EWMS.Session:** NoResult → endpoint [Authorize] trả 401.
3. `h → a`: GET /api/auth/me + cookie.
4. `a → v`: ValidatePrincipal(context).
5. `v → r`: FindByIdAsync(userId).
6. `r → d`: SELECT User JOIN Role WHERE UserID=@id.
7. `v` xử lý: Kiểm ACTIVE, role được hỗ trợ, kho nếu WAREHOUSE_STAFF và row_version.
8. **Nhánh lỗi — User/claims không còn hợp lệ:** RejectPrincipal + SignOut; /me trả 401 → caller dừng.
9. `h` xử lý: Tạo ClaimsPrincipal cho service hiện tại.
10. `c` xử lý: [Authorize] kiểm requiredRole; phạm vi kho kiểm ở Actor khi cần.
11. **Nhánh lỗi — Sai quyền / không được phân công kho:** 403; không chạy nghiệp vụ.
12. **opt [requireCsrf = true]**.
13. `c → h`: SessionCsrfFilter.OnActionExecutionAsync().
14. **Nhánh lỗi — Thiếu X-CSRF-TOKEN:** 400; không gọi action.
15. `h → a`: POST /api/auth/validate-csrf + cookie + token.
16. **ref SD02.SESSION**: Lặp kiểm principal tại Identity cho request này.
17. `a` xử lý: antiforgery.IsRequestValidAsync(HttpContext).
18. **Nhánh lỗi — Phiên/CSRF sai:** 400/401/403 → filter trả lỗi về caller, dừng action.

Ký hiệu lifeline: `c` = Service nhận request / Middleware / filter; `h` = IdentitySessionHandler / SessionCsrfFilter; `a` = Identity.API / AuthController / Cookie Auth; `v` = SessionValidation; `r` = UserRepository; `d` = IdentityDB / User + Role.

**Ghi chú nghiệp vụ:**

- Lời gọi nội bộ đi thẳng Identity.API, không vòng qua WMS.API. Lỗi Identity/SQL → 503. SD02 được gọi trước mỗi endpoint được bảo vệ; ref SESSION chỉ là đoạn kiểm cookie, không gọi đệ quy toàn bộ SD02.

**Nguồn code:** [WMS.ServiceDefaults/ServiceSecurity.cs](../WMS.ServiceDefaults/ServiceSecurity.cs), [Identity.API/Authentication/SessionValidation.cs](../Identity.API/Authentication/SessionValidation.cs).

### SD03 — Đăng nhập

**Phân công:** Người 1 • Authentication. **Feature:** Authentication. **Class diagram:** CD01 — Authentication.

**Tiền điều kiện:** Người dùng cung cấp username/password; không bắt buộc đã có phiên.

**Hậu điều kiện:** Thành công: cookie EWMS.Session + UserProfile; frontend chuyển trang. Sai: không tạo phiên.

**Lifeline:** Người dùng → Frontend → WMS.API / ApiServiceProxy → Identity.API / AuthController → LoginService → PasswordService → UserRepository → IdentityDB. Đây là danh sách thành phần, không khẳng định mọi lời gọi đi tuyến tính theo danh sách.

**Luồng chính và điểm tái sử dụng:**

1. `u → f`: Nhập username, password, rememberMe.
2. **ref SD01**: Lấy token trước POST login.
3. `f → g`: POST /api/auth/login + cookie CSRF + X-CSRF-TOKEN.
4. `g → c`: Login(LoginRequest).
5. `c` xử lý: Kiểm model, rate limit và CSRF bằng IAntiforgery.
6. **Nhánh lỗi — Dữ liệu/CSRF sai hoặc vượt rate limit:** 400 hoặc 429 → frontend hiển thị lỗi.
7. `c → s`: LoginAsync(username, password).
8. `s → r`: FindByUsernameAsync(username.Trim()).
9. `r → d`: SELECT User JOIN Role theo username.
10. `s → p`: Verify(user.PasswordHash hoặc DummyHash, password).
11. **Nhánh lỗi — Không có user hoặc sai mật khẩu:** LoginResult lỗi → 401 qua gateway.
12. `s` xử lý: AccessError(user): ACTIVE, role hợp lệ, warehouse hợp lệ.
13. **Nhánh lỗi — Không được phép truy cập:** LoginResult(Forbidden=true) → 403.
14. **opt [SuccessRehashNeeded]**.
15. `s → p`: Hash(password).
16. `s → r`: UpdatePasswordHashAsync(user, hash).
17. `r → d`: UPDATE User theo UserID + RowVersion.
18. `s → r`: FindByIdAsync(user.UserId).
19. `r → d`: SELECT user hiện tại.
20. `s` xử lý: Kiểm lại tồn tại + AccessError.
21. `s → p`: Verify(hash hiện tại, password).
22. **Nhánh lỗi — User thay đổi / không còn hợp lệ:** 401; không tạo phiên.
23. `c` xử lý: Tạo claims; HttpContext.SignInAsync() → cookie EWMS.Session.
24. `f` xử lý: AuthProvider.setUser(profile); navigate theo role.

Ký hiệu lifeline: `u` = Người dùng; `f` = Frontend; `g` = WMS.API / ApiServiceProxy; `c` = Identity.API / AuthController; `s` = LoginService; `p` = PasswordService; `r` = UserRepository; `d` = IdentityDB.

**Ghi chú nghiệp vụ:**

- Login chưa có phiên nên không ref SD02. RememberMe: 14 ngày persistent; không chọn: session cookie, ticket 8 giờ. Claims và cookie là xử lý của controller/framework, không phải model User.

**Nguồn code:** [frontend/src/features/auth/components/login-form.tsx](../frontend/src/features/auth/components/login-form.tsx), [Identity.API/Controllers/AuthController.cs](../Identity.API/Controllers/AuthController.cs), [Identity.BLL/Authentication/LoginService.cs](../Identity.BLL/Authentication/LoginService.cs), [Identity.DAL/Repositories/UserRepository.cs](../Identity.DAL/Repositories/UserRepository.cs).

### SD04 — Tạo / sửa Purchase Order

**Phân công:** Người 1 • PO; CREATE là luồng đại diện. **Feature:** Purchase Order. **Class diagram:** CD02 — Purchase Order.

**Tiền điều kiện:** PURCHASING_STAFF. UPDATE có id + RowVersion của PO DRAFT chưa có inbound.

**Hậu điều kiện:** CREATE trả 201 PO DRAFT; UPDATE trả 200 PO và version mới. Header, lines và audit cùng transaction.

**Lifeline:** Người dùng → Frontend → WMS.API / ApiServiceProxy → Procurement.API / PurchaseOrdersController → PurchaseOrderService → PurchaseOrderLookupClient → Catalog / Warehouse API / Lookup repositories → CatalogDB / WarehouseDB → PurchaseOrderRepository → ProcurementDB. Đây là danh sách thành phần, không khẳng định mọi lời gọi đi tuyến tính theo danh sách.

**Luồng chính và điểm tái sử dụng:**

1. `u → f`: Nhập thông tin và lưu PO.
2. **ref SD01**: Frontend lấy token.
3. `f → g`: POST /api/purchase-orders (CREATE) hoặc PUT /api/purchase-orders/{id} (UPDATE).
4. `g → c`: Create(request) / Update(id, request).
5. **ref SD02**: requiredRole=PURCHASING_STAFF; requireCsrf=true.
6. `c → s`: CreateAsync / UpdateAsync(request, actor).
7. **opt [UPDATE]**.
8. `s → r`: Existing(id, RowVersion) → FindAsync(id).
9. `r → d`: SELECT PurchaseOrder + PurchaseOrderLine.
10. **ref SD06.RECEIVING**: Kiểm receiving summary; yêu cầu DRAFT và chưa có Inbound.
11. **Nhánh lỗi — Sai version/state hoặc đã có phiếu nhập:** 400/404/409; dừng.
12. `s → l`: GetOptionsAsync().
13. **par [Catalog và Warehouse gọi đồng thời]**.
14. `l → x`: GET /api/catalog/options + cookie.
15. `x → xd`: CatalogRepository.GetAsync → SELECT supplier/product.
16. **and [Warehouse]**.
17. `l → x`: GET /api/warehouses/options + cookie.
18. `x → xd`: WarehouseRepository.GetAsync → SELECT kho hoạt động.
19. `s` xử lý: Validate(request/options); Normalize(note) 1..500 dòng; quantity>0; đúng supplier; ngày hợp lệ.
20. **Nhánh lỗi — Dữ liệu sai:** 400 + errors.
21. **alt [CREATE; dùng lại cho import]**.
22. `s → r`: CreateAsync(request, userId, name, action, importKey?).
23. `r → d`: BEGIN TRANSACTION.
24. **opt [importKey có giá trị]**.
25. `d` xử lý: sp_getapplock(userId, importKey); SELECT AuditLog tìm PO đã import.
26. `d` xử lý: Nếu chưa tồn tại: INSERT PurchaseOrder(DRAFT); INSERT PurchaseOrderLine; INSERT AuditLog(action).
27. **else [UPDATE: cấu trúc gọi giống CREATE, SQL khác]**.
28. `s → r`: UpdateAsync(expected, normalizedRequest, actor).
29. `r → d`: BEGIN; LockAndCheck RowVersion/DRAFT; UPDATE header + INSERT/UPDATE/DELETE lines; AuditLog(UPDATE_PO); COMMIT.

Ký hiệu lifeline: `u` = Người dùng; `f` = Frontend; `g` = WMS.API / ApiServiceProxy; `c` = Procurement.API / PurchaseOrdersController; `s` = PurchaseOrderService; `l` = PurchaseOrderLookupClient; `x` = Catalog / Warehouse API / Lookup repositories; `xd` = CatalogDB / WarehouseDB; `r` = PurchaseOrderRepository; `d` = ProcurementDB.

**Ghi chú nghiệp vụ:**

- Nếu đã có importKey: commit, đọc PO cũ và trả lại; bỏ qua INSERT bên dưới. Nhánh này chống tạo trùng khi retry import.
- UPDATE gọi GetCreatorNameAsync qua Identity trước khi ghi; CREATE dùng tên người tạo từ claims. WRITE_CREATE chỉ đại diện phần repository, không gọi lại frontend/controller. Mỗi lần gọi có transaction riêng.

**Nguồn code:** [Procurement.API/Controllers/PurchaseOrdersController.cs](../Procurement.API/Controllers/PurchaseOrdersController.cs), [Procurement.BLL/PurchaseOrders/PurchaseOrderService.cs](../Procurement.BLL/PurchaseOrders/PurchaseOrderService.cs), [Procurement.API/Clients/PurchaseOrderLookupClient.cs](../Procurement.API/Clients/PurchaseOrderLookupClient.cs), [Procurement.DAL/PurchaseOrders/PurchaseOrderRepository.cs](../Procurement.DAL/PurchaseOrders/PurchaseOrderRepository.cs).

### SD05 — Import Purchase Order từ Excel

**Phân công:** Người 2 • Preview và confirm import. **Feature:** Purchase Order. **Class diagram:** CD02 — dùng lại từ SD04.

**Tiền điều kiện:** PURCHASING_STAFF; file .xlsx ≤2 MB; confirm dùng token preview của đúng user và còn hạn.

**Hậu điều kiện:** Preview không tạo PO. Confirm import trả PO DRAFT, retry không tạo thêm với cùng token/key.

**Lifeline:** Nhân viên mua hàng → Frontend / purchaseOrderApi → WMS.API / ApiServiceProxy → PurchaseOrdersController → PurchaseOrderService → PurchaseOrderWorkbook → ImportPreviewStore / Entry → PurchaseOrderRepository → ProcurementDB. Đây là danh sách thành phần, không khẳng định mọi lời gọi đi tuyến tính theo danh sách.

**Luồng chính và điểm tái sử dụng:**

1. `u → f`: Chọn file .xlsx và xem trước.
2. **ref SD01**: Lấy token.
3. `f → g`: POST /api/purchase-orders/import/preview multipart/form-data.
4. `g → c`: Preview(file).
5. **ref SD02**: PURCHASING_STAFF; requireCsrf=true.
6. `c` xử lý: Kiểm file tồn tại, .xlsx, không rỗng, ≤2 MB.
7. **Nhánh lỗi — File không hợp lệ:** 400.
8. `c → s`: PreviewAsync(stream, userId).
9. `s → w`: Parse(stream).
10. **ref SD04.OPTIONS**: Lấy danh mục mới để kiểm supplier, kho, product.
11. `s` xử lý: Kiểm từng dòng, 1..500 dòng, không trùng, cùng thông tin header, quantity/ngày hợp lệ.
12. **alt [Preview không hợp lệ]**.
13. **else [Preview hợp lệ]**.
14. `s → p`: Add(userId, CreatePurchaseOrder).
15. `u → f`: Nhấn Xác nhận import với token hợp lệ.
16. **ref SD01**: Lấy token CSRF mới.
17. `f → g`: POST /api/purchase-orders/import/confirm { token }.
18. `g → c`: Confirm(ConfirmImport).
19. **ref SD02**: PURCHASING_STAFF; requireCsrf=true.
20. `c → s`: ConfirmAsync(token, userId, userName).
21. `s → p`: Get(token, userId).
22. **Nhánh lỗi — Token sai / hết hạn / khác user:** 409 ImportConflictException.
23. `s → p`: Entry.Gate.WaitAsync().
24. **alt [Entry.CreatedId đã có]**.
25. `s → r`: FindAsync(createdId) qua service.FindAsync.
26. `r → d`: SELECT PO + Lines.
27. **else [Chưa tạo; kiểm lại ExpiresAt]**.
28. **ref SD04.OPTIONS**: Lấy danh mục và Validate(entry.Request).
29. `s` xử lý: Validate(entry.Request, options); importKey = SHA256(token).
30. **ref SD04.WRITE_CREATE**: request=entry.Request; action=IMPORT_PO_EXCEL; importKey=SHA256(token). Nhận StoredPurchaseOrder..
31. `s` xử lý: entry.CreatedId = order.PurchaseOrderId.
32. `s` xử lý: finally: entry.Gate.Release() kể cả khi có lỗi.

Ký hiệu lifeline: `u` = Nhân viên mua hàng; `f` = Frontend / purchaseOrderApi; `g` = WMS.API / ApiServiceProxy; `c` = PurchaseOrdersController; `s` = PurchaseOrderService; `w` = PurchaseOrderWorkbook; `p` = ImportPreviewStore / Entry; `r` = PurchaseOrderRepository; `d` = ProcurementDB.

**Ghi chú nghiệp vụ:**

- Trả ImportPreview(Valid=false, Token=null, Rows, Errors); không lưu entry, không ghi PO.
- Thao tác tiếp theo chỉ thực hiện khi Valid=true. Preview chỉ lưu bộ nhớ, chưa ghi database. Tải template là GET → TemplateAsync → OPTIONS → workbook.CreateTemplate, không tạo PO.
- service.FindAsync bổ sung ReceivingSummary, options và tên người tạo như SD06.RECEIVING / SD04.OPTIONS; không INSERT thêm.
- Confirm import tạo DRAFT. DRAFT → CONFIRMED là SD06, không phải thao tác này. Lỗi workbook/validation=400; preview conflict=409; nguồn dịch vụ=503.

**Nguồn code:** [Procurement.BLL/PurchaseOrders/PurchaseOrderService.cs](../Procurement.BLL/PurchaseOrders/PurchaseOrderService.cs), [Procurement.Excel/PurchaseOrderWorkbook.cs](../Procurement.Excel/PurchaseOrderWorkbook.cs), [Procurement.DAL/PurchaseOrders/PurchaseOrderRepository.cs](../Procurement.DAL/PurchaseOrders/PurchaseOrderRepository.cs).

### SD06 — Xác nhận / hủy / đóng Purchase Order

**Phân công:** Người 2 • CONFIRM là thao tác đại diện. **Feature:** Purchase Order. **Class diagram:** CD02 — dùng lại từ SD04.

**Tiền điều kiện:** PURCHASING_STAFF; có PO/version; cancel/close có reason.

**Hậu điều kiện:** PO chuyển CONFIRMED/CANCELLED/CLOSED hoặc trả lỗi; audit ghi cùng transaction.

**Lifeline:** Nhân viên mua hàng → Frontend / purchaseOrderApi → WMS.API / ApiServiceProxy → PurchaseOrdersController → PurchaseOrderService → PurchaseOrderLookupClient → Inventory.API / ReceivingSummaryController → ReceivingSummaryRepository → InventoryDB → PurchaseOrderRepository → ProcurementDB. Đây là danh sách thành phần, không khẳng định mọi lời gọi đi tuyến tính theo danh sách.

**Luồng chính và điểm tái sử dụng:**

1. `u → f`: Chọn confirm / cancel / close.
2. **ref SD01**: Lấy token CSRF.
3. `f → g`: POST /api/purchase-orders/{id}/{action} { rowVersion, reason? }.
4. `g → c`: ConfirmOrder / Cancel / Close.
5. **ref SD02**: PURCHASING_STAFF; requireCsrf=true.
6. `c → s`: ConfirmOrderAsync / CancelAsync / CloseAsync.
7. `s → r`: Existing(id, version) → FindAsync(id).
8. `r → d`: SELECT PO + Lines.
9. `s` xử lý: Kiểm version + trạng thái; Cancel/Close kiểm reason 1..1000 ký tự.
10. **Nhánh lỗi — Không tồn tại / version sai / trạng thái sai:** 400/404/409.
11. `s → l`: GetReceivingSummaryAsync(poId).
12. `l → ic`: GET /api/inventory/purchase-orders/{id}/receiving-summary.
13. **ref SD02**: Tại Inventory: PURCHASING_STAFF; requireCsrf=false.
14. `ic → ir`: GetAsync(poId).
15. `ir → idb`: SELECT Inbound; đếm serial từ InboundItem + SerialItem.
16. `s` xử lý: ValidateReceiving(order, receiving).
17. **ref SD04.OPTIONS**: GetOptionsAsync; CONFIRM dùng để Validate lại PO.
18. `s` xử lý: Lấy tên người tạo qua lookup.GetCreatorNameAsync (Identity.API → UserRepository → IdentityDB).
19. `s → r`: TransitionAsync(expected, targetStatus, reason, closedQuantities, actor).
20. `r → d`: BEGIN; LockAndCheck: SELECT PO WITH(UPDLOCK,HOLDLOCK).
21. `d` xử lý: Kiểm lại RowVersion và chuyển trạng thái hợp lệ.
22. **opt [target=CANCELLED hoặc CLOSED]**.
23. `d` xử lý: CANCELLED: không có bất kỳ ReceivingReservation. CLOSED: không có ReceivingReservation OPEN..
24. **Nhánh lỗi — Reservation đang ngăn thao tác:** 409; dispose transaction → rollback.
25. **opt [target=CLOSED]**.
26. `d` xử lý: Kiểm closedQuantities; UPDATE PurchaseOrderLine.ClosedQuantity.
27. `d` xử lý: UPDATE PO status/timestamps/reason; INSERT AuditLog(CONFIRM_PO/CANCEL_PO/CLOSE_PO).

Ký hiệu lifeline: `u` = Nhân viên mua hàng; `f` = Frontend / purchaseOrderApi; `g` = WMS.API / ApiServiceProxy; `c` = PurchaseOrdersController; `s` = PurchaseOrderService; `l` = PurchaseOrderLookupClient; `ic` = Inventory.API / ReceivingSummaryController; `ir` = ReceivingSummaryRepository; `idb` = InventoryDB; `r` = PurchaseOrderRepository; `d` = ProcurementDB.

**Ghi chú nghiệp vụ:**

- CONFIRM: DRAFT, chưa có Inbound, Validate lại PO. CANCEL: DRAFT/CONFIRMED, chưa có bất kỳ Inbound. CLOSE: CONFIRMED, không còn Inbound mở; closedQuantity=ordered−received. Các điều kiện không đạt → 400/409, không ghi.
- Không vẽ ba sequence trùng nhau: thay action, điều kiện và targetStatus theo bảng trên. Update PO dùng SD04, vì sửa dòng hàng khác TransitionAsync.

**Nguồn code:** [Procurement.BLL/PurchaseOrders/PurchaseOrderService.cs](../Procurement.BLL/PurchaseOrders/PurchaseOrderService.cs), [Procurement.DAL/PurchaseOrders/PurchaseOrderRepository.cs](../Procurement.DAL/PurchaseOrders/PurchaseOrderRepository.cs), [Inventory.DAL/Receiving/ReceivingSummaryRepository.cs](../Inventory.DAL/Receiving/ReceivingSummaryRepository.cs).

### SD07 — Tạo phiếu Inbound từ PO

**Phân công:** Người 3 • Tạo DRAFT và giữ reservation. **Feature:** Inbound. **Class diagram:** CD03 — Inbound.

**Tiền điều kiện:** WAREHOUSE_STAFF được gán kho; PO CONFIRMED còn lượng nhận và thỏa policy DRAFT.

**Hậu điều kiện:** Phiếu DRAFT + reservation; token trùng hợp lệ trả lại phiếu cũ.

**Lifeline:** Nhân viên kho → Frontend / inboundApi → WMS.API / ApiServiceProxy → Inventory.API / InboundsController → InboundRepository → InventoryDB → ReceivingOrderClient → ReceivingOrdersController → Procurement repositories → ProcurementDB. Đây là danh sách thành phần, không khẳng định mọi lời gọi đi tuyến tính theo danh sách.

**Luồng chính và điểm tái sử dụng:**

1. `u → f`: Chọn PO và tạo phiếu nhập.
2. **ref SD01**: Lấy token CSRF.
3. `f → g`: POST /api/inbounds { purchaseOrderId, note, token UUID }.
4. `g → c`: Create(request).
5. **ref SD02**: WAREHOUSE_STAFF; requireCsrf=true; kiểm kho Actor.
6. `c` xử lý: Validate token / PO id / note.
7. `c → r`: FindByTokenAsync(token, actor).
8. `r → d`: SELECT Inbound theo token + warehouse.
9. **Nhánh lỗi — Token trùng PO khác hoặc phiếu CANCELLED:** 409.
10. `c → l`: FindAsync(purchaseOrderId).
11. `l → pc`: GET /api/receiving-orders/{id} + cookie.
12. **ref SD02**: WAREHOUSE_STAFF; requireCsrf=false.
13. `pc → pr`: IPurchaseOrderRepository.FindAsync(id).
14. `pr → pd`: SELECT PurchaseOrder + PurchaseOrderLine.
15. `pc` xử lý: Kiểm đúng kho; lookup.GetOptionsAsync lấy tên Catalog/Warehouse qua API + database sở hữu.
16. `c → r`: ListAsync(actor) / ProgressAsync(order).
17. `r → d`: SELECT Inbound/InboundItem/SerialItem; tính received/reserved/remaining.
18. **Nhánh lỗi — PO chưa CONFIRMED / không còn quantity / DRAFT trái policy:** 409.
19. `c → l`: ReserveAsync(poId, token).
20. `l → pc`: POST /api/receiving-orders/{id}/reserve.
21. **ref SD02**: WAREHOUSE_STAFF; requireCsrf=true (header chuyển tiếp).
22. `pc → pr`: ReceivingReservationRepository.ReserveAsync.
23. `pr → pd`: BEGIN; khóa PO; INSERT ReceivingReservation; Touch PO RowVersion; COMMIT.
24. `c → r`: CreateAsync(order, note, actor, token).
25. `r → d`: BEGIN + sp_getapplock EWMS:InboundWrite; kiểm token/draft/progress; INSERT Inbound(DRAFT); AuditLog(INBOUND_CREATED); COMMIT.
26. `c → r`: FindAsync(id, actor).
27. `r → d`: SELECT Inbound đúng kho.
28. **ref SD07.ORDER**: orders.FindAsync(header.PurchaseOrderId).
29. `c → r`: ProgressAsync(order, id) + ItemsAsync(id).
30. `r → d`: SELECT tiến độ + InboundItem hoạt động.
31. `c` xử lý: new ReceiptDetail(header, order, progress, items, policy).

Ký hiệu lifeline: `u` = Nhân viên kho; `f` = Frontend / inboundApi; `g` = WMS.API / ApiServiceProxy; `c` = Inventory.API / InboundsController; `r` = InboundRepository; `d` = InventoryDB; `l` = ReceivingOrderClient; `pc` = ReceivingOrdersController; `pr` = Procurement repositories; `pd` = ProcurementDB.

**Ghi chú nghiệp vụ:**

- Nếu token đã có phiếu hợp lệ: ref SD07.LOAD rồi trả 200; bỏ qua reserve/create. Các bước tiếp theo chỉ chạy khi token chưa có.
- Nếu CreateAsync ném ReceiptException: AbortCreationAsync khóa cùng phạm vi để chặn retry muộn. Khi có bằng chứng terminal, ref SD09.RELEASE để giải phóng reservation, rồi trả lỗi.

**Nguồn code:** [Inventory.API/Controllers/InboundsController.cs](../Inventory.API/Controllers/InboundsController.cs), [Inventory.API/Clients/ReceivingOrderClient.cs](../Inventory.API/Clients/ReceivingOrderClient.cs), [Inventory.DAL/Inbound/InboundRepository.cs](../Inventory.DAL/Inbound/InboundRepository.cs), [Procurement.API/Controllers/ReceivingOrdersController.cs](../Procurement.API/Controllers/ReceivingOrdersController.cs).

### SD08 — Thêm / sửa / xóa serial trong Inbound

**Phân công:** Người 3 • ADD là thao tác đại diện. **Feature:** Inbound. **Class diagram:** CD03 — dùng lại từ SD07.

**Tiền điều kiện:** WAREHOUSE_STAFF đúng kho; phiếu DRAFT và RowVersion hiện tại.

**Hậu điều kiện:** Item được thêm/sửa/vô hiệu hóa; phiếu có version mới. Chưa tạo SerialItem thật.

**Lifeline:** Nhân viên kho → Frontend / inboundApi → WMS.API / ApiServiceProxy → InboundsController → InboundRules → InboundRepository → InventoryDB. Đây là danh sách thành phần, không khẳng định mọi lời gọi đi tuyến tính theo danh sách.

**Luồng chính và điểm tái sử dụng:**

1. `u → f`: Quét/nhập serial hoặc sửa/xóa dòng.
2. **ref SD01**: Lấy CSRF token.
3. `f → g`: POST .../{id}/items; PUT .../{id}/items/{itemId}; DELETE .../{id}/items/{itemId}.
4. `g → c`: Add / Update / Remove.
5. **ref SD02**: WAREHOUSE_STAFF; requireCsrf=true; đúng kho.
6. **opt [ADD / UPDATE]**.
7. `c → r`: Header(id) → FindAsync(id, actor).
8. `r → d`: SELECT Inbound đúng kho.
9. **ref SD07.ORDER**: Sau đọc Header, lấy PO hiện tại của phiếu.
10. `c → r`: SaveItemAsync(id, itemId?, request, order, actor) hoặc RemoveItemAsync(id, itemId, rowVersion, actor).
11. **opt [ADD / UPDATE]**.
12. `r → b`: Validate(request, policy, Today()).
13. `r → d`: BEGIN + khóa EWMS:InboundWrite; SELECT Inbound WITH(UPDLOCK,HOLDLOCK).
14. `d` xử lý: Đúng kho, DRAFT, RowVersion khớp.
15. **Nhánh lỗi — Không tìm thấy / sai version hoặc state:** 404/409; rollback, trả lỗi qua controller.
16. **alt [ADD / UPDATE]**.
17. `d` xử lý: Kiểm POLine thuộc PO; serial chưa trùng; RemainingQuantity đủ.
18. `d` xử lý: INSERT InboundItem (ADD) hoặc UPDATE InboundItem (UPDATE).
19. **else [REMOVE]**.
20. `d` xử lý: Kiểm item đang hoạt động; UPDATE InboundItem SET IsActive=0.
21. `d` xử lý: Touch Inbound để đổi RowVersion; AuditLog(INBOUND_SERIAL_ADDED / UPDATED / REMOVED).
22. **ref SD07.LOAD**: Load(id) lấy ReceiptDetail + RowVersion mới.

Ký hiệu lifeline: `u` = Nhân viên kho; `f` = Frontend / inboundApi; `g` = WMS.API / ApiServiceProxy; `c` = InboundsController; `b` = InboundRules; `r` = InboundRepository; `d` = InventoryDB.

**Ghi chú nghiệp vụ:**

- Không vẽ ba cấu trúc CRUD lặp lại. REMOVE không gọi InboundRules.Validate(request) và không lấy PO; vẫn kiểm DRAFT/version. Save chỉ ghi InboundItem; SerialItem được tạo ở SD09 khi complete.

**Nguồn code:** [Inventory.API/Controllers/InboundsController.cs](../Inventory.API/Controllers/InboundsController.cs), [Inventory.BLL/Inbound/InboundRules.cs](../Inventory.BLL/Inbound/InboundRules.cs), [Inventory.DAL/Inbound/InboundRepository.cs](../Inventory.DAL/Inbound/InboundRepository.cs).

### SD09 — Hoàn tất / hủy Inbound và release reservation

**Phân công:** Người 4 • COMPLETE là thao tác đại diện. **Feature:** Inbound. **Class diagram:** CD03 — dùng lại từ SD07.

**Tiền điều kiện:** WAREHOUSE_STAFF đúng kho. Complete cần ≥1 serial hợp lệ; cancel cần policy cho phép + reason.

**Hậu điều kiện:** DONE tạo hàng chờ putaway, hoặc CANCELLED; release reservation sau khi kiểm proof terminal.

**Lifeline:** Nhân viên kho → Frontend / inboundApi → WMS.API / ApiServiceProxy → InboundsController → InboundRepository / ReceiptQueries → InventoryDB → ReceivingOrderClient → ReceivingOrdersController → ReceivingReservationRepository → ProcurementDB. Đây là danh sách thành phần, không khẳng định mọi lời gọi đi tuyến tính theo danh sách.

**Luồng chính và điểm tái sử dụng:**

1. `u → f`: Chọn hoàn tất hoặc hủy phiếu.
2. **ref SD01**: CSRF token.
3. `f → g`: POST /api/inbounds/{id}/complete hoặc /cancel { rowVersion, reason? }.
4. `g → c`: Complete / Cancel.
5. **ref SD02**: WAREHOUSE_STAFF; requireCsrf=true; đúng kho.
6. `c → r`: FindAsync(id, actor).
7. `r → d`: SELECT Inbound.
8. **alt [COMPLETE và chưa DONE]**.
9. **ref SD07.ORDER**: Lấy PO hiện tại để kiểm và hoàn tất phiếu.
10. `c → r`: CompleteAsync(id, rowVersion, order, actor).
11. `r → d`: BEGIN + khóa; kiểm DRAFT/version, có ≥1 serial, serial hợp lệ, không trùng, không vượt PO.
12. `d` xử lý: INSERT SupplySource theo product + condition; INSERT SerialItem(IN_WAREHOUSE, CurrentLocationID=NULL).
13. `d` xử lý: UPDATE InboundItem.SerialItemID; INSERT SerialMovement(PURCHASE_RECEIPT).
14. `d` xử lý: UPDATE Inbound='DONE', CompletedAt, actor; INSERT AuditLog(INBOUND_COMPLETED).
15. **else [CANCEL và chưa CANCELLED]**.
16. `c → r`: CancelAsync(id, rowVersion, reason, actor).
17. `r` xử lý: Kiểm policy.AllowCancellation + reason.
18. `r → d`: BEGIN + khóa; kiểm DRAFT/version; UPDATE InboundItem.IsActive=0; Inbound='CANCELLED'; AuditLog(INBOUND_CANCELLED).
19. `c → r`: ReceiptQueries.TokenAsync(id, warehouse).
20. `r → d`: SELECT CreationToken.
21. **opt [token có giá trị]**.
22. `c → l`: ReleaseAsync(poId, token).
23. `l → pc`: POST /api/receiving-orders/{id}/release.
24. **ref SD02**: Tại Procurement: WAREHOUSE_STAFF; requireCsrf=true.
25. `pc → r`: HTTP GET /api/inventory/receiving-reservations/{token} qua ReceivingQueriesController → ReceiptQueries.ReservationAsync.
26. `r → d`: SELECT PO, Warehouse, Status theo token.
27. `pc` xử lý: Kiểm Exists + Terminal + PO/kho khớp.
28. **Nhánh lỗi — Không có bằng chứng terminal đúng PO/kho:** 409; không release.
29. `pc → pr`: ReleaseAsync(poId, token, warehouse).
30. `pr → pd`: BEGIN; UPDATE ReceivingReservation='RELEASED'; Touch PO RowVersion; COMMIT.
31. **ref SD07.LOAD**: Lấy ReceiptDetail mới.

Ký hiệu lifeline: `u` = Nhân viên kho; `f` = Frontend / inboundApi; `g` = WMS.API / ApiServiceProxy; `c` = InboundsController; `r` = InboundRepository / ReceiptQueries; `d` = InventoryDB; `l` = ReceivingOrderClient; `pc` = ReceivingOrdersController; `pr` = ReceivingReservationRepository; `pd` = ProcurementDB.

**Ghi chú nghiệp vụ:**

- Retry complete khi đã DONE / cancel khi đã CANCELLED: bỏ qua mutation; vẫn thực hiện RELEASE và LOAD. Khi chưa terminal, chạy phần bên dưới.
- Inventory đã commit trước HTTP release. Release lỗi không rollback hàng đã nhận; retry dùng trạng thái terminal để tránh nhận lần hai. Lỗi nghiệp vụ=400/403/404/409; nguồn dữ liệu=503. Các transaction chưa commit sẽ rollback.

**Nguồn code:** [Inventory.DAL/Inbound/InboundRepository.cs](../Inventory.DAL/Inbound/InboundRepository.cs), [Inventory.DAL/Receiving/ReceiptQueries.cs](../Inventory.DAL/Receiving/ReceiptQueries.cs), [Procurement.API/Controllers/ReceivingOrdersController.cs](../Procurement.API/Controllers/ReceivingOrdersController.cs), [Procurement.DAL/Receiving/ReceivingReservationRepository.cs](../Procurement.DAL/Receiving/ReceivingReservationRepository.cs).

### SD10 — Tra cứu serial và gợi ý vị trí Putaway

**Phân công:** Người 4 • Suggestions; pending/scan dùng cùng cấu trúc đọc. **Feature:** Putaway. **Class diagram:** CD04 — Putaway.

**Tiền điều kiện:** WAREHOUSE_STAFF đúng kho; tìm serial hoặc chọn từ danh sách pending.

**Hậu điều kiện:** Serial + candidates hợp lệ/không hợp lệ và lý do; không ghi vị trí hoặc giữ chỗ.

**Lifeline:** Nhân viên kho → Frontend / putawayApi → WMS.API / ApiServiceProxy → PutawayController → PutawayService → PutawayRepository → PutawayRules / CurrentSchemaPutawayGuard → SQL Server / InventoryDB / CatalogDB / WarehouseDB. Đây là danh sách thành phần, không khẳng định mọi lời gọi đi tuyến tính theo danh sách.

**Luồng chính và điểm tái sử dụng:**

1. `u → f`: Quét serial hoặc chọn serial chờ putaway.
2. `f → g`: GET /api/putaway/serials?serialNumber=... hoặc /pending?search&page&pageSize.
3. `g → c`: ScanQuery / Pending.
4. **ref SD02**: WAREHOUSE_STAFF; requireCsrf=false.
5. `c → s`: FindAsync / PendingAsync.
6. `s` xử lý: Validate serial hoặc page/search.
7. `s → r`: FindAsync / PendingAsync.
8. `r → d`: SELECT SerialItem + Inbound, JOIN CatalogDB product và WarehouseDB.Warehouse; lọc kho.
9. `f → g`: GET /api/putaway/{id}/suggestions.
10. `g → c`: Suggestions(id).
11. **ref SD02**: WAREHOUSE_STAFF; requireCsrf=false.
12. `c → s`: SuggestionsAsync(actor, id).
13. `s → r`: SuggestionsAsync(actor, id).
14. `r → d`: AssertSupportedControls; ReadItem; Snapshot Warehouse/StorageLocation/ZoneStorageRule.
15. `r → b`: ValidateSerial(item, warehouse); guard.ValidateAsync.
16. `r → d`: Occupants(warehouse, location=null); JOIN CatalogDB để tính kích thước / tải trọng.
17. `r → b`: Suggest(item, snapshot, occupants).

Ký hiệu lifeline: `u` = Nhân viên kho; `f` = Frontend / putawayApi; `g` = WMS.API / ApiServiceProxy; `c` = PutawayController; `s` = PutawayService; `r` = PutawayRepository; `b` = PutawayRules / CurrentSchemaPutawayGuard; `d` = SQL Server / InventoryDB / CatalogDB / WarehouseDB.

**Ghi chú nghiệp vụ:**

- SQL cross-database trên cùng instance, không gọi Catalog.API/Warehouse.API. locked=true dùng transaction và HOLDLOCK/UPDLOCK; SD11 tự đọc Occupants sau kiểm version/guard.
- Serial hợp lệ: ở kho được giao, IN_WAREHOUSE, chưa có location; nguồn PURCHASE phải có Inbound DONE. Schema claim/stocktake chưa tích hợp → 503. Gợi ý chưa giữ chỗ; SD11 kiểm lại khi ghi.

**Nguồn code:** [Inventory.API/Controllers/PutawayController.cs](../Inventory.API/Controllers/PutawayController.cs), [Inventory.BLL/Putaway/PutawayService.cs](../Inventory.BLL/Putaway/PutawayService.cs), [Inventory.BLL/Putaway/PutawayRules.cs](../Inventory.BLL/Putaway/PutawayRules.cs), [Inventory.DAL/Putaway/PutawayRepository.cs](../Inventory.DAL/Putaway/PutawayRepository.cs).

### SD11 — Xác nhận Putaway

**Phân công:** Người 5 • Ghi vị trí, movement và audit trong transaction. **Feature:** Putaway. **Class diagram:** CD04 — dùng lại từ SD10.

**Tiền điều kiện:** Đã biết serial, targetLocation và RowVersion; WAREHOUSE_STAFF. SD10 là tiền điều kiện, không phải luôn gọi lại khi confirm.

**Hậu điều kiện:** Cập nhật location + movement + audit nguyên tử; lỗi trước commit rollback.

**Lifeline:** Nhân viên kho → Frontend / putawayApi → WMS.API / ApiServiceProxy → PutawayController → PutawayService → PutawayRepository → PutawayRules / OperationGuard → SQL Server / InventoryDB / CatalogDB / WarehouseDB. Đây là danh sách thành phần, không khẳng định mọi lời gọi đi tuyến tính theo danh sách.

**Luồng chính và điểm tái sử dụng:**

1. `u → f`: Chọn vị trí và nhấn Xác nhận.
2. **ref SD10**: Tiền điều kiện: đã tra cứu/gợi ý; chỉ chạy lại nếu thiếu dữ liệu. Sử dụng serialItemId + targetLocationId + RowVersion..
3. **ref SD01**: Lấy CSRF token.
4. `f → g`: POST /api/putaway { serialItemId, targetLocationId, rowVersion }.
5. `g → c`: Confirm(request).
6. **ref SD02**: WAREHOUSE_STAFF; requireCsrf=true.
7. `c → s`: ConfirmAsync(actor, request).
8. `s` xử lý: Kiểm ID và PutawayRules.Version(rowVersion).
9. `s → r`: ConfirmAsync(actor, request).
10. `r → d`: BEGIN ReadCommitted; SET LOCK_TIMEOUT; sp_getapplock theo warehouse + targetLocation.
11. **ref SD10.READ_CONTEXT**: locked=true; cùng connection/transaction đang mở.
12. `r → b`: ValidateSerial(item, actor.WarehouseId).
13. `r` xử lý: So khớp RowVersion hiện tại với request.
14. `r → b`: guard.ValidateAsync(item, snapshot).
15. `r` xử lý: Tìm targetLocation trong snapshot.
16. **Nhánh lỗi — Serial/version/kho/target không hợp lệ:** 400/404/409; transaction dispose → rollback.
17. `r → d`: Occupants(targetLocation) WITH(HOLDLOCK).
18. `r → b`: Evaluate(item, snapshot, target, occupants).
19. **Nhánh lỗi — Vị trí không hợp lệ / không đủ sức chứa:** 409 LOCATION_UNAVAILABLE; rollback.
20. `r → d`: UPDATE SerialItem.CurrentLocationID WHERE location NULL + đúng kho/state + RowVersion.
21. `r → d`: INSERT SerialMovement(EventType=PUTAWAY); INSERT AuditLog(PUTAWAY_COMPLETED); COMMIT.

Ký hiệu lifeline: `u` = Nhân viên kho; `f` = Frontend / putawayApi; `g` = WMS.API / ApiServiceProxy; `c` = PutawayController; `s` = PutawayService; `r` = PutawayRepository; `b` = PutawayRules / OperationGuard; `d` = SQL Server / InventoryDB / CatalogDB / WarehouseDB.

**Ghi chú nghiệp vụ:**

- SD10 là tiền điều kiện, không bắt buộc gọi lại suggestions khi bấm Confirm. Mọi lỗi trước commit rollback cả UPDATE/movement/audit. Putaway không cộng tồn kho lần nữa.

**Nguồn code:** [Inventory.BLL/Putaway/PutawayService.cs](../Inventory.BLL/Putaway/PutawayService.cs), [Inventory.DAL/Putaway/PutawayRepository.cs](../Inventory.DAL/Putaway/PutawayRepository.cs).

### SD12 — Xem danh sách và chi tiết Audit Log

**Phân công:** Người 5 • Cùng sơ đồ đọc cho nguồn inventory/procurement. **Feature:** Xem Audit Log. **Class diagram:** CD05 — View Audit Log.

**Tiền điều kiện:** ADMIN có phiên hợp lệ; bộ lọc/source/id hợp lệ.

**Hậu điều kiện:** Danh sách audit tổng hợp hoặc chi tiết đã sanitize; lỗi nguồn trả 503.

**Lifeline:** Admin → Frontend / auditApi → WMS.API / AdminAuditController → AuditGateway → AuditLogsController / Inventory hoặc Procurement → SqlAuditReader → Database nguồn / InventoryDB hoặc ProcurementDB → Identity.API + DAL / AuthController / UserRepository → IdentityDB. Đây là danh sách thành phần, không khẳng định mọi lời gọi đi tuyến tính theo danh sách.

**Luồng chính và điểm tái sử dụng:**

1. `u → f`: Mở audit log, chọn bộ lọc.
2. `f → c`: GET /api/admin/audit-logs?query.
3. **ref SD02**: Tại WMS.API: ADMIN; requireCsrf=false.
4. `c → g`: ListAsync(query).
5. `g` xử lý: query.Validate(); dựng query string.
6. **Nhánh lỗi — Bộ lọc/cursor sai:** 400 + errors.
7. **par [Fan-out: inventory + procurement + employees]**.
8. `g → a`: GET /api/audit-logs?query (mode=page) hoặc /{id} (detail), /actions (actions).
9. **ref SD02**: Mỗi service nguồn: ADMIN; requireCsrf=false.
10. `a → r`: PageAsync(query) / DetailAsync(id) / ActionsAsync().
11. `r → d`: page: COUNT + SELECT TOP(PageSize+1) + cursor; detail: SELECT theo id + Old/New; actions: SELECT DISTINCT Action.
12. `r` xử lý: Map DTO; detail: Sanitize(OldValues/NewValues).
13. **and [Employees]**.
14. `g → i`: GET /api/auth/audit-users + cookie.
15. `i → idb`: ListDisplayNamesAsync → SELECT UserID, Username, FullName.
16. `g` xử lý: Task.WhenAll; gộp và sort CreatedAt DESC, Source ASC, ID DESC; Take(PageSize); FillActor; TotalCount + HasMore + NextCursor.
17. `u → f`: Chọn bản ghi để xem trước/sau.
18. `f → c`: GET /api/admin/audit-logs/{source}/{id}.
19. **ref SD02**: ADMIN; requireCsrf=false.
20. `c → g`: DetailAsync(source, id).
21. **Nhánh lỗi — Source/id không hợp lệ:** null → 404.
22. **ref SD12.SOURCE_READ**: mode=detail; source được chọn; chỉ đọc một nguồn.
23. **Nhánh lỗi — Nguồn trả 404:** null → 404; không lấy employees.
24. **ref SD12.EMPLOYEES**: Bổ sung actor name nếu thiếu.
25. `g` xử lý: FillActor; Redact(OldValues, NewValues).

Ký hiệu lifeline: `u` = Admin; `f` = Frontend / auditApi; `c` = WMS.API / AdminAuditController; `g` = AuditGateway; `a` = AuditLogsController / Inventory hoặc Procurement; `r` = SqlAuditReader; `d` = Database nguồn / InventoryDB hoặc ProcurementDB; `i` = Identity.API + DAL / AuthController / UserRepository; `idb` = IdentityDB.

**Ghi chú nghiệp vụ:**

- SOURCE_READ được thực thi 2 lần đồng thời cho mode=page: source=inventory và source=procurement. Chỉ vẽ cấu trúc một lần; hai service có cùng AuditReadControllerBase + SqlAuditReader.
- WMS.API xử lý endpoint admin cục bộ và tổng hợp; không proxy endpoint này sang một service duy nhất. Lỗi một nguồn → 503, không trả dữ liệu thiếu nguồn.
- Options: GET /options → OptionsAsync → SOURCE_READ(mode=actions) trên hai nguồn + EMPLOYEES song song; gộp KnownActions, distinct/sort. Không cần sequence thứ 13 vì cùng cấu trúc fan-out đã mô tả.

**Nguồn code:** [WMS.API/Controllers/AdminAuditController.cs](../WMS.API/Controllers/AdminAuditController.cs), [WMS.API/Audit/AuditGateway.cs](../WMS.API/Audit/AuditGateway.cs), [WMS.Audit/AuditReadControllerBase.cs](../WMS.Audit/AuditReadControllerBase.cs), [WMS.Audit/SqlAuditReader.cs](../WMS.Audit/SqlAuditReader.cs).

## Bảng biến thể: cùng cấu trúc thì dẫn lại, không vẽ thêm

| Nhóm | Diagram chuẩn | Điểm khác cần ghi trong báo cáo |
|---|---|---|
| Create/Update PO | SD04 | POST/PUT; Update cần Existing/version + chưa có inbound; SQL thay dòng thay vì INSERT đơn mới |
| Import PO | SD05 → SD04.WRITE_CREATE | Preview không ghi; confirm truyền IMPORT_PO_EXCEL + importKey |
| Confirm/Cancel/Close PO | SD06 | Đổi allowed state, reason, receiving guard, reservation guard và targetStatus |
| Add/Update/Remove serial | SD08 | Remove không lấy PO/Validate SaveReceiptItem; chỉ soft delete; tất cả kiểm DRAFT/version |
| Complete/Cancel Inbound | SD09 | Complete tạo SupplySource/SerialItem/movement; cancel soft delete; dùng chung RELEASE + LOAD |
| Pending/Scan Putaway | SD10 | Khác query/filter/DTO; suggestions có thêm rules và occupants |
| Inventory/Procurement audit | SD12.SOURCE_READ | Cùng base controller/reader, đổi source và connectionString; không nhân đôi diagram |
| Audit options | SD12 | Cùng fan-out nguồn + employees; mode=actions rồi distinct/sort |

## Mẫu câu để đưa vào báo cáo

> **Feature: Import Purchase Order.** The model class diagram is reused from Purchase Order (CD02), with the corresponding Class Specifications referenced from the shared model specification section. The preview and confirmation interaction is shown in SD05. The database creation interaction is reused through ref SD04.WRITE_CREATE with action=IMPORT_PO_EXCEL and importKey=SHA256(token).

> **Function: Cancel Purchase Order.** Reuse SD06 and CD02. Compared with Confirm Purchase Order, the allowed states are DRAFT/CONFIRMED; a reason is required; existing inbound data and any receiving reservation block cancellation; the target state is CANCELLED.

## Kiểm tra trước khi nộp

1. Đổi Người 1…5 thành tên thật. Thống nhất với thầy cách tính 10 sơ đồ cá nhân + 2 sơ đồ dùng chung.
2. Trong mỗi feature ghi đủ ba mục: Class Diagram → Class Specifications → Sequence Diagram(s).
3. Trích lại bằng mã CD/CS/SD; không chèn cùng hình nhiều lần.
4. Khi xuất PDF/ảnh từ draw.io, chọn đúng trang và zoom đủ đọc. Trong báo cáo có thể dẫn ref bằng mã vì liên kết trang draw.io không còn hoạt động như trong editor.
5. Đọc nhánh lỗi cùng điều kiện; không coi mọi alt/opt là các bước bắt buộc nối tiếp.

## Tham chiếu

[Class diagram](wms-domain-class-diagrams.drawio) · [Class Specifications](model-class-specifications.md) · [12 sequence diagrams](wms-sequence-study-12.drawio) · [Kiến trúc gateway](api-service.md).

