-- EWMS local demo master data. Safe to run again. Does not create orders or modify accounts/warehouses.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

DECLARE @creator int = (
    SELECT TOP (1) u.UserID FROM IdentityDB.dbo.[User] u
    JOIN IdentityDB.dbo.[Role] r ON r.RoleID = u.RoleID
    WHERE r.RoleName = 'ADMIN' AND u.Status = 'ACTIVE' ORDER BY u.UserID
);
IF @creator IS NULL
    THROW 50001, N'Cần tạo tài khoản ADMIN ACTIVE trước khi seed sản phẩm.', 1;

USE CatalogDB;

DECLARE @brands TABLE (Name nvarchar(100));
INSERT @brands VALUES (N'Daikin'), (N'Samsung'), (N'Panasonic'), (N'Electrolux'), (N'Sharp');
INSERT dbo.Brand (BrandName, Status)
SELECT Name, 'ACTIVE' FROM @brands b
WHERE NOT EXISTS (SELECT 1 FROM dbo.Brand WITH (UPDLOCK, HOLDLOCK) WHERE BrandName = b.Name);

DECLARE @categories TABLE (Name nvarchar(100), Description nvarchar(1000));
INSERT @categories VALUES
    (N'Điều hòa', N'Thiết bị điều hòa không khí'),
    (N'Ti vi', N'Thiết bị truyền hình'),
    (N'Tủ lạnh', N'Thiết bị bảo quản thực phẩm'),
    (N'Bếp từ', N'Thiết bị nấu ăn bằng cảm ứng từ'),
    (N'Nồi cơm điện', N'Thiết bị nấu cơm điện');
INSERT dbo.Category (CategoryName, Description)
SELECT Name, Description FROM @categories c
WHERE NOT EXISTS (SELECT 1 FROM dbo.Category WITH (UPDLOCK, HOLDLOCK) WHERE CategoryName = c.Name);

DECLARE @suppliers TABLE (Name nvarchar(200), Address nvarchar(500), Email nvarchar(254));
INSERT @suppliers VALUES
    (N'Nhà cung cấp Demo Bắc', N'Hà Nội', N'demo-bac@example.com'),
    (N'Nhà cung cấp Demo Nam', N'Thành phố Hồ Chí Minh', N'demo-nam@example.com');
INSERT dbo.Supplier (SupplierName, Address, Email, Status)
SELECT Name, Address, Email, 'ACTIVE' FROM @suppliers s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Supplier WITH (UPDLOCK, HOLDLOCK) WHERE SupplierName = s.Name);

DECLARE @products TABLE (
    OldCode varchar(40), Code varchar(40), Name nvarchar(200), Model nvarchar(100),
    Category nvarchar(100), Brand nvarchar(100), LengthCm decimal(10,2),
    WidthCm decimal(10,2), HeightCm decimal(10,2), WeightKg decimal(10,3)
);
INSERT @products VALUES
    ('DEMO-LAP-001', 'DEMO-AC-001', N'Điều hòa Daikin 1 HP', N'AC-1HP-DEMO', N'Điều hòa', N'Daikin', 85, 35, 30, 35.000),
    ('DEMO-LAP-002', 'DEMO-AC-002', N'Điều hòa Panasonic 1.5 HP', N'AC-1.5HP-DEMO', N'Điều hòa', N'Panasonic', 95, 38, 35, 42.000),
    ('DEMO-MON-001', 'DEMO-TV-001', N'Ti vi Samsung 55 inch', N'TV-55-DEMO', N'Ti vi', N'Samsung', 138, 85, 18, 20.000),
    ('DEMO-MON-002', 'DEMO-FRIDGE-001', N'Tủ lạnh Panasonic 300 lít', N'FRIDGE-300L-DEMO', N'Tủ lạnh', N'Panasonic', 70, 72, 180, 65.000),
    ('DEMO-ACC-001', 'DEMO-IND-001', N'Bếp từ đôi Electrolux', N'IND-2ZONE-DEMO', N'Bếp từ', N'Electrolux', 82, 52, 18, 12.000),
    ('DEMO-ACC-002', 'DEMO-RICE-001', N'Nồi cơm điện Sharp 1.8 lít', N'RICE-1.8L-DEMO', N'Nồi cơm điện', N'Sharp', 38, 35, 35, 4.500);

-- Migrate the original demo rows in place, preserving ProductID and supplier/PO references.
IF EXISTS (
    SELECT 1 FROM @products source
    JOIN dbo.Product oldProduct ON oldProduct.ProductCode = source.OldCode
    JOIN dbo.Product newProduct ON newProduct.ProductCode = source.Code
    WHERE oldProduct.ProductID <> newProduct.ProductID
)
    THROW 50002, N'Mã sản phẩm mới đã tồn tại ở bản ghi khác. Không thay đổi dữ liệu.', 1;

UPDATE product
SET ProductCode = source.Code, ProductName = source.Name, Model = source.Model,
    CategoryID = category.CategoryID, BrandID = brand.BrandID, Unit = N'Chiếc',
    Specifications = N'Dữ liệu mẫu thiết bị gia dụng; model, kích thước và trọng lượng đóng gói là giá trị minh họa.',
    LengthCm = source.LengthCm, WidthCm = source.WidthCm, HeightCm = source.HeightCm,
    GrossWeightKg = source.WeightKg, UpdatedAt = SYSUTCDATETIME()
FROM dbo.Product product JOIN @products source ON product.ProductCode = source.OldCode
CROSS APPLY (SELECT TOP (1) CategoryID FROM dbo.Category WHERE CategoryName = source.Category ORDER BY CategoryID) category
CROSS APPLY (SELECT TOP (1) BrandID FROM dbo.Brand WHERE BrandName = source.Brand ORDER BY BrandID) brand;

INSERT dbo.Product (CategoryID, BrandID, CreatedByUserID, ProductCode, ProductName,
    Model, Unit, Specifications, LengthCm, WidthCm, HeightCm, GrossWeightKg, Status, CreatedAt)
SELECT category.CategoryID, brand.BrandID, @creator, p.Code, p.Name, p.Model, N'Chiếc',
    N'Dữ liệu mẫu thiết bị gia dụng; model, kích thước và trọng lượng đóng gói là giá trị minh họa.',
    p.LengthCm, p.WidthCm, p.HeightCm, p.WeightKg, 'ACTIVE', SYSUTCDATETIME()
FROM @products p
CROSS APPLY (SELECT TOP (1) CategoryID FROM dbo.Category WHERE CategoryName = p.Category ORDER BY CategoryID) category
CROSS APPLY (SELECT TOP (1) BrandID FROM dbo.Brand WHERE BrandName = p.Brand ORDER BY BrandID) brand
WHERE NOT EXISTS (SELECT 1 FROM dbo.Product WITH (UPDLOCK, HOLDLOCK) WHERE ProductCode = p.Code);

-- Both demo suppliers can supply the six demo products, allowing either receiving warehouse.
INSERT dbo.SupplierProduct (SupplierID, ProductID)
SELECT s.SupplierID, p.ProductID FROM dbo.Supplier s
CROSS JOIN dbo.Product p
WHERE s.SupplierName IN (N'Nhà cung cấp Demo Bắc', N'Nhà cung cấp Demo Nam')
  AND p.ProductCode IN (SELECT Code FROM @products)
  AND NOT EXISTS (SELECT 1 FROM dbo.SupplierProduct sp WITH (UPDLOCK, HOLDLOCK)
                  WHERE sp.SupplierID = s.SupplierID AND sp.ProductID = p.ProductID);

-- Remove obsolete demo labels only if neither products nor warehouse rules still reference them.
DELETE category FROM dbo.Category category
WHERE category.CategoryName IN (N'Laptop', N'Màn hình', N'Phụ kiện')
  AND NOT EXISTS (SELECT 1 FROM dbo.Product p WHERE p.CategoryID = category.CategoryID)
  AND NOT EXISTS (SELECT 1 FROM WarehouseDB.dbo.ZoneStorageRule r WHERE r.CategoryID = category.CategoryID);
DELETE brand FROM dbo.Brand brand
WHERE brand.BrandName IN (N'Dell', N'Logitech')
  AND NOT EXISTS (SELECT 1 FROM dbo.Product p WHERE p.BrandID = brand.BrandID)
  AND NOT EXISTS (SELECT 1 FROM WarehouseDB.dbo.ZoneStorageRule r WHERE r.BrandID = brand.BrandID);

COMMIT TRANSACTION;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
SELECT SupplierID, SupplierName FROM dbo.Supplier WHERE SupplierName IN (N'Nhà cung cấp Demo Bắc', N'Nhà cung cấp Demo Nam');
SELECT ProductID, ProductCode, ProductName FROM dbo.Product WHERE ProductCode IN (SELECT Code FROM @products) ORDER BY ProductCode;
