-- Run against ProcurementDB before deploying the Inbound module. Additive, rerunnable migration.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.ReceivingReservation','U') IS NULL
BEGIN
    CREATE TABLE dbo.ReceivingReservation (
        Token uniqueidentifier NOT NULL CONSTRAINT PK_ReceivingReservation PRIMARY KEY,
        PurchaseOrderID bigint NOT NULL,
        WarehouseID int NOT NULL,
        CreatedByUserID int NOT NULL,
        Status varchar(16) NOT NULL,
        CreatedAt datetime2(3) NOT NULL,
        ReleasedAt datetime2(3) NULL,
        CONSTRAINT FK_ReceivingReservation_PO FOREIGN KEY(PurchaseOrderID) REFERENCES dbo.PurchaseOrder(PurchaseOrderID),
        CONSTRAINT CK_ReceivingReservation_Status CHECK
            ((Status='OPEN' AND ReleasedAt IS NULL) OR (Status='RELEASED' AND ReleasedAt IS NOT NULL))
    );
    CREATE INDEX IX_ReceivingReservation_PO ON dbo.ReceivingReservation(PurchaseOrderID,Status);
END;
COMMIT;
