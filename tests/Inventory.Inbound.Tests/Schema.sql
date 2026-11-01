-- Reduced fixture of the existing EWMS source schema. Relevant original constraints and indices retained.
CREATE TABLE dbo.Inbound(
 InboundID bigint IDENTITY PRIMARY KEY,PurchaseOrderID bigint NOT NULL,CreatedByUserID int NOT NULL,
 PerformedByUserID int NULL,ReceiptCode varchar(40) NOT NULL UNIQUE,ReceiptDate date NOT NULL,
 Status varchar(32) NOT NULL,CreatedAt datetime2(3) NOT NULL,CompletedAt datetime2(3) NULL,
 Note nvarchar(1000) NULL,RowVersion rowversion NOT NULL,
 CONSTRAINT CK_Inbound_01 CHECK(Status<>'DONE' OR (CompletedAt IS NOT NULL AND PerformedByUserID IS NOT NULL)),
 CONSTRAINT CK_Inbound_02 CHECK(Status IN('DRAFT','DONE','CANCELLED')));
CREATE TABLE dbo.SupplySource(
 SupplySourceID bigint IDENTITY PRIMARY KEY,ProductID int NOT NULL,WarehouseID int NOT NULL,
 ItemCondition varchar(8) NOT NULL,SourceType varchar(24) NOT NULL,SupplyState varchar(8) NOT NULL,
 POLineID bigint NULL,InboundID bigint NULL REFERENCES dbo.Inbound(InboundID),ReturnReceiptID bigint NULL,
 TransferReceiptID bigint NULL,AdjustmentID bigint NULL,EligibleQuantity int NOT NULL,RemainingQuantity int NOT NULL,
 AvailableFrom date NULL,CreatedAt datetime2(3) NOT NULL,RowVersion rowversion NOT NULL,
 CONSTRAINT CK_SupplySource_02 CHECK(SourceType='INBOUND' AND SupplyState='PHYSICAL' AND InboundID IS NOT NULL AND POLineID IS NULL),
 CONSTRAINT CK_SupplySource_03 CHECK(ItemCondition IN('GOOD','DAMAGED')),
 CONSTRAINT CK_SupplySource_04 CHECK(RemainingQuantity>=0 AND EligibleQuantity>=0 AND EligibleQuantity<=RemainingQuantity));
CREATE TABLE dbo.SerialItem(
 SerialItemID bigint IDENTITY PRIMARY KEY,ProductID int NOT NULL,SerialNumber nvarchar(100) NOT NULL UNIQUE,
 ManufacturingMonth tinyint NULL,ManufacturingYear smallint NULL,OriginType varchar(24) NOT NULL,
 OriginalInboundID bigint NULL REFERENCES dbo.Inbound(InboundID),OriginAdjustmentID bigint NULL,
 CurrentWarehouseID int NULL,CurrentLocationID int NULL,CurrentSupplySourceID bigint NULL REFERENCES dbo.SupplySource(SupplySourceID),
 StockState varchar(32) NOT NULL,ItemCondition varchar(8) NOT NULL,IsQuarantined bit NOT NULL,
 QuarantineReason nvarchar(1000) NULL,RegisteredAt datetime2(3) NOT NULL,RowVersion rowversion NOT NULL,
 CONSTRAINT CK_SerialItem_01 CHECK((ManufacturingMonth IS NULL AND ManufacturingYear IS NULL) OR (ManufacturingMonth BETWEEN 1 AND 12 AND ManufacturingYear BETWEEN 1900 AND 9999)),
 CONSTRAINT CK_SerialItem_02 CHECK(OriginType='PURCHASE' AND OriginalInboundID IS NOT NULL AND OriginAdjustmentID IS NULL),
 CONSTRAINT CK_SerialItem_03 CHECK((StockState='IN_WAREHOUSE' AND CurrentWarehouseID IS NOT NULL AND CurrentSupplySourceID IS NOT NULL)
 OR (StockState<>'IN_WAREHOUSE' AND CurrentWarehouseID IS NULL AND CurrentLocationID IS NULL AND CurrentSupplySourceID IS NULL)),
 CONSTRAINT CK_SerialItem_04 CHECK(ItemCondition IN('GOOD','DAMAGED')),
 CONSTRAINT CK_SerialItem_05 CHECK(IsQuarantined=0 OR (QuarantineReason IS NOT NULL AND LEN(LTRIM(RTRIM(QuarantineReason)))>0)),
 CONSTRAINT CK_SerialItem_06 CHECK(StockState IN('IN_WAREHOUSE','IN_TRANSIT','SOLD','LOST')));
CREATE TABLE dbo.InboundItem(
 InboundItemID bigint IDENTITY PRIMARY KEY,InboundID bigint NOT NULL REFERENCES dbo.Inbound(InboundID),POLineID bigint NOT NULL,
 ScannedSerial nvarchar(100) NOT NULL,SerialItemID bigint NULL REFERENCES dbo.SerialItem(SerialItemID),
 ManufacturingMonth tinyint NULL,ManufacturingYear smallint NULL,ReceivedCondition varchar(8) NULL,
 ConditionNote nvarchar(1000) NULL,ScannedAt datetime2(3) NOT NULL,IsActive bit NOT NULL,
 CONSTRAINT CK_InboundItem_01 CHECK(ManufacturingMonth IS NULL OR ManufacturingMonth BETWEEN 1 AND 12),
 CONSTRAINT CK_InboundItem_02 CHECK(ManufacturingYear IS NULL OR ManufacturingYear BETWEEN 1900 AND 9999),
 CONSTRAINT CK_InboundItem_03 CHECK(ReceivedCondition IS NULL OR ReceivedCondition IN('GOOD','DAMAGED')));
CREATE UNIQUE INDEX UX_InboundItem_01 ON dbo.InboundItem(ScannedSerial) WHERE IsActive=1;
CREATE UNIQUE INDEX UX_InboundItem_02 ON dbo.InboundItem(SerialItemID) WHERE SerialItemID IS NOT NULL;
CREATE TABLE dbo.SerialMovement(
 MovementID bigint IDENTITY PRIMARY KEY,SerialItemID bigint NOT NULL REFERENCES dbo.SerialItem(SerialItemID),PerformedByUserID int NOT NULL,
 EventType varchar(32) NOT NULL,OccurredAt datetime2(3) NOT NULL,FromState varchar(16) NULL,ToState varchar(16) NOT NULL,
 FromCondition varchar(8) NULL,ToCondition varchar(8) NOT NULL,FromWarehouseID int NULL,ToWarehouseID int NULL,
 FromLocationID int NULL,ToLocationID int NULL,FromQuarantined bit NULL,ToQuarantined bit NOT NULL,
 InboundID bigint NULL REFERENCES dbo.Inbound(InboundID),OutboundID bigint NULL,ReturnReceiptID bigint NULL,
 DispatchID bigint NULL,TransferReceiptID bigint NULL,AdjustmentID bigint NULL,LossRequestID bigint NULL,Note nvarchar(1000) NULL,
 CONSTRAINT CK_SerialMovement_01 CHECK(EventType='PURCHASE_RECEIPT'),
 CONSTRAINT CK_SerialMovement_02 CHECK(ToState IN('IN_WAREHOUSE','IN_TRANSIT','SOLD','LOST')),
 CONSTRAINT CK_SerialMovement_03 CHECK(ToCondition IN('GOOD','DAMAGED')),
 CONSTRAINT CK_SerialMovement_06 CHECK(InboundID IS NOT NULL AND OutboundID IS NULL AND ReturnReceiptID IS NULL AND DispatchID IS NULL AND TransferReceiptID IS NULL AND AdjustmentID IS NULL AND LossRequestID IS NULL));
CREATE TABLE dbo.AuditLog(AuditLogID bigint IDENTITY PRIMARY KEY,UserID int NOT NULL,Action varchar(60) NOT NULL,
 EntityName varchar(100) NOT NULL,EntityID nvarchar(100) NOT NULL,OldValues nvarchar(max) NULL,NewValues nvarchar(max) NULL,
 Status varchar(20) NOT NULL,CreatedAt datetime2(3) NOT NULL);
