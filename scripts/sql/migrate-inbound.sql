-- Run against InventoryDB. Additive migration; no business rows are deleted or fabricated.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.Inbound','U') IS NULL OR OBJECT_ID('dbo.InboundItem','U') IS NULL
    THROW 50001, 'Existing Inventory Inbound schema is required.', 1;
IF COL_LENGTH('dbo.Inbound','WarehouseID') IS NULL
    ALTER TABLE dbo.Inbound ADD WarehouseID int NULL;
IF COL_LENGTH('dbo.Inbound','CreatedByName') IS NULL
    ALTER TABLE dbo.Inbound ADD CreatedByName nvarchar(200) NULL;
IF COL_LENGTH('dbo.Inbound','PerformedByName') IS NULL
    ALTER TABLE dbo.Inbound ADD PerformedByName nvarchar(200) NULL;
IF COL_LENGTH('dbo.Inbound','CreationToken') IS NULL
    ALTER TABLE dbo.Inbound ADD CreationToken uniqueidentifier NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Inbound') AND name='UX_Inbound_CreationToken')
    EXEC('CREATE UNIQUE INDEX UX_Inbound_CreationToken ON dbo.Inbound(CreationToken) WHERE CreationToken IS NOT NULL');
IF COL_LENGTH('dbo.InboundItem','InputMethod') IS NULL
    ALTER TABLE dbo.InboundItem ADD InputMethod varchar(16) NOT NULL
        CONSTRAINT DF_InboundItem_InputMethod DEFAULT ('UNKNOWN') WITH VALUES;
IF COL_LENGTH('dbo.InboundItem','ScannedByUserID') IS NULL
    ALTER TABLE dbo.InboundItem ADD ScannedByUserID int NULL;
IF COL_LENGTH('dbo.InboundItem','ScannedByName') IS NULL
    ALTER TABLE dbo.InboundItem ADD ScannedByName nvarchar(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_InboundItem_InputMethod')
    EXEC('ALTER TABLE dbo.InboundItem WITH CHECK ADD CONSTRAINT CK_InboundItem_InputMethod CHECK (InputMethod IN (''MANUAL'',''CAMERA'',''IMAGE_UPLOAD'',''UNKNOWN''))');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.InboundItem') AND name='UX_InboundItem_01')
    CREATE UNIQUE INDEX UX_InboundItem_01 ON dbo.InboundItem(ScannedSerial) WHERE IsActive=1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.InboundItem') AND name='UX_InboundItem_02')
    CREATE UNIQUE INDEX UX_InboundItem_02 ON dbo.InboundItem(SerialItemID) WHERE SerialItemID IS NOT NULL;
COMMIT;
-- Legacy receipt WarehouseID is intentionally NULL. Reconcile it through the owning PO API
-- before backfilling; assigning every historic receipt to a guessed warehouse is unsafe.
