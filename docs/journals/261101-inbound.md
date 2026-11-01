---
date: 2026-11-01
topic: inbound-management
status: implemented-with-verification-limitations
---
# Hoàn thiện tiếp nhận Inbound theo Serial

## Bối cảnh

Yêu cầu hoàn thiện Inbound từ tài liệu Word, dựa trên PO hiện có và kiến trúc 5 database của EWMS. Đã đọc prompt, đặc tả nghiệp vụ v2 và schema SQL thay vì dựng lại ứng dụng. Không có Git checkout ở workspace, không tạo commit.

## Thay đổi

Thêm mô hình nhận hàng, backend validation, repository SQL, API nhận từ PO, màn hình list/create/detail và chờ Putaway. Serial có tháng/năm sản xuất, tình trạng, phương thức nhập và người nhập. Manual, camera ZXing và ảnh barcode/OCR đều cần người dùng xác nhận rồi mới lưu.

Inventory transaction khóa ghi, kiểm tra RowVersion, chống trùng và vượt PO. Khi hoàn tất, nguồn hàng, SerialItem, liên kết dòng, movement và audit được commit cùng trạng thái DONE. Thống kê đọc cả DONE lẫn COMPLETED lịch sử, sửa lệch trạng thái trong code cũ. Chờ Putaway dùng IN_WAREHOUSE với vị trí NULL theo schema hiện có.

Procurement reservation bền vững bảo vệ khoảng trống giữa hai service để PO không đóng/hủy khi đang tạo hoặc xử lý receipt. Retry token tạo phiếu ngăn dữ liệu trùng. Bù trừ khi tạo thất bại ghi CANCELLED rỗng dưới cùng khóa Inventory, giữ lại DRAFT nếu nó đã được tạo, rồi mới cho giải phóng reservation theo bằng chứng terminal.

Migration bổ sung cột và bảng reservation, không xóa dữ liệu. Script reconciliation lấy warehouse từ PO sở hữu cho phiếu lịch sử, không đoán kho hoặc tạo metadata người scan giả. Phương thức nhập của dữ liệu lịch sử chưa biết được đánh dấu UNKNOWN. Đã bổ sung nút Đồng bộ trạng thái PO cho phiếu terminal để retry sau lỗi giải phóng reservation mà không ghi nhận tồn kho lần nữa.

## Nhận xét và quyết định

Khó nhất là đồng thời giữa PO và receipt trong hai database. HTTP đọc trạng thái đơn thuần không ngăn race, vì vậy cần reservation được ghi trước Inventory mutation và kiểm tra dưới khóa PO. Reservation giữ nguyên khi kết quả chưa rõ giúp tránh cho phép đóng PO quá sớm; retry cần sử dụng cùng token.

Người dùng đã xác nhận chỉ một DRAFT mỗi PO, từ chối hàng hỏng nặng, nhận hàng hỏng nhẹ/móp méo vào cách ly, và cho hủy DRAFT khi có lý do. Cấu hình tương ứng AllowMultipleDrafts=false, AcceptDamaged=true, AllowCancellation=true. DAMAGED biểu diễn hàng hỏng nhẹ đã được nhân viên chấp nhận, yêu cầu mô tả và không khả dụng; lựa chọn hỏng nặng không lưu Serial. Không thêm severity schema. Không bổ sung expiry, serial nội bộ hoặc thuật toán đặt vị trí trong phạm vi này.

## Kiểm chứng và việc tiếp theo

Solution build thành công, 0 lỗi/cảnh báo. Inbound rules/API và SQL integration trên database tạm thực tại MIDUKONGU đã pass; Procurement API/SQL, Identity, Excel và audit regression suites pass. Migration đã áp dụng thành công vào ProcurementDB/InventoryDB local, không có phiếu lịch sử cần reconciliation được báo ra. Kết nối SQL lỗi SSPI ban đầu đã được khắc phục trong môi trường có quyền thích hợp. Lần build có restore ban đầu bị chặn đọc NuGet.Config; no-restore dùng cache hiện có được.

Frontend 41/41 tests và production build pass sau khi cập nhật chính sách đã chốt. Kiểm tra Chrome thật với 8 ảnh desktop/mobile dùng API mock không có tràn ngang/lỗi JavaScript. Có lỗi source có sẵn ở Catalog.Products.Tests: ProductTests.cs dòng 65 tham chiếu SqlProductTests nhưng thiếu file/class trong bản source ban đầu. Không tuyên bố toàn bộ regression suites đã pass.

Kế hoạch triển khai đã đồng bộ hoàn tất; cần thử camera điện thoại qua HTTPS cùng ảnh tem thật; OCR assets cần internet. Chính sách nghiệp vụ đã được người dùng xác nhận. Tài liệu vận hành/API/migration nằm ở `docs/inbound.md`, manifest kiểm chứng ở `plans/261101-inbound/reports/verification.md`. Không commit vì workspace không có .git và git không có trên PATH; người dùng chưa yêu cầu commit.

