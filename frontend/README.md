# Frontend WMS

Frontend React + TypeScript, chạy bằng Vite. Trang mặc định là giao diện đăng nhập EWMS.

## Chạy ứng dụng

Mở terminal tại thư mục `frontend`, chạy `npm install`, sau đó `npm run dev` và mở địa chỉ terminal hiển thị. Dùng `npm run build` để kiểm tra TypeScript và build; `npm run preview` để xem bản build.

Trang đăng nhập gọi Identity.API thông qua WMS.API, dùng cookie HttpOnly và phân quyền admin, mua hàng, kho. Ghi nhớ đăng nhập giữ phiên 14 ngày; phiên thường tối đa 8 giờ. Nhân viên kho cần được gán WarehouseID.

Nhân viên mua hàng có danh sách, tạo PO nháp thủ công, chi tiết PO và import Excel có xem trước/kiểm tra lỗi/xác nhận. Xem hướng dẫn đầy đủ tại [docs/purchase-orders.md](../docs/purchase-orders.md). Chạy `scripts/start-ewms.ps1` từ thư mục solution để khởi động đủ sáu backend; Vite proxy chuyển toàn bộ `/api` đến WMS.API tại `http://localhost:5062`, rồi WMS chuyển tiếp đến service phụ trách. Cần khởi động lại backend và Vite nếu đang dùng cấu hình cũ. Xem [API Service](../docs/api-service.md) về bảng định tuyến và cấu hình production.

## Tổ chức

- `public/`: tài nguyên tĩnh được phục vụ trực tiếp.
- `src/app/routes/`: định tuyến và bảo vệ route.
- `src/app/providers/`: các provider dùng toàn ứng dụng.
- `src/layouts/`: khung trang, header và sidebar.
- `src/features/`: các nghiệp vụ auth, catalog, warehouse, procurement và inventory.
- `src/shared/components/ui/`: thành phần giao diện cơ bản dùng chung.
- `src/shared/components/common/`: thành phần dùng chung như bảng, phân trang và trạng thái tải.
- `src/shared/hooks/`: hook dùng chung.
- `src/shared/lib/`: tích hợp thư viện và HTTP client chung.
- `src/shared/utils/`: hàm tiện ích dùng chung.
- `src/shared/types/`: kiểu dữ liệu dùng chung.
- `src/shared/config/`: cấu hình ứng dụng.
- `src/assets/`: hình ảnh và icon được import trong ứng dụng.
- `src/styles/`: style dùng toàn ứng dụng.

## Cấu trúc mỗi feature

- `pages/`: các màn hình của nghiệp vụ.
- `components/`: thành phần chỉ dùng trong nghiệp vụ.
- `api/`: các hàm gọi API của nghiệp vụ.
- `hooks/`: hook của nghiệp vụ.
- `types/`: kiểu dữ liệu của nghiệp vụ.
- `schemas/`: quy tắc kiểm tra dữ liệu đầu vào.

## Quy tắc phụ thuộc

`app` sử dụng `features`, `layouts` và `shared`. `features` và `layouts` sử dụng `shared`. `shared` không import ngược từ ba nhóm còn lại.

Giữ logic đặc thù trong feature tương ứng. Chỉ đưa thành phần vào `shared` khi cần dùng chung. Chưa tạo feature sales vì phạm vi backend hiện chưa có nghiệp vụ này.

## Nhập kho

Nhân viên kho mở `/warehouse/inbounds` để tạo phiếu từ PO đã xác nhận, nhận Serial bằng nhập tay/camera/ảnh tem và hoàn tất để chuyển hàng sang chờ Putaway. Mỗi PO chỉ có một phiếu DRAFT; hàng hỏng nặng bị từ chối, hàng móp nhẹ được nhận vào cách ly; hủy phiếu cần lý do. Xem [hướng dẫn Inbound](../docs/inbound.md) về migration, API và giới hạn kiểm chứng trên điện thoại.
