export interface SupplierOption { supplierId: number; supplierName: string }
export interface WarehouseOption { warehouseId: number; warehouseName: string; address: string }
export interface ProductOption {
  productId: number; productCode: string; productName: string; unit: string; supplierIds: number[];
}
export interface PurchaseOrderOptions { suppliers: SupplierOption[]; warehouses: WarehouseOption[]; products: ProductOption[] }
export interface PurchaseOrderSummary {
  purchaseOrderId: number; poCode: string; supplierId: number; supplierName: string;
  warehouseId: number; warehouseName: string; orderDate: string; expectedArrivalDate: string | null;
  status: string; createdAt: string; lineCount: number; totalQuantity: number;
}
export interface PurchaseOrderDetail extends PurchaseOrderSummary {
  createdByUserId: number; createdByName: string; note: string | null;
  rowVersion: string; confirmedAt: string | null; closedAt: string | null; closeReason: string | null;
  hasAnyInbound: boolean; openInboundCount: number;
  lines: { poLineId: number; productId: number; productCode: string; productName: string; unit: string; quantity: number; closedQuantity: number; receivedQuantity: number; remainingQuantity: number }[];
}
export interface CreatePurchaseOrder {
  supplierId: number; warehouseId: number; expectedArrivalDate: string | null; note: string | null;
  lines: { productId: number; quantity: number }[];
}
export interface ImportPreview {
  token: string | null; valid: boolean; errors: string[];
  rows: {
    rowNumber: number; supplierId?: number | null; warehouseId?: number | null;
    expectedArrivalDate?: string | null; note?: string | null; productCode: string;
    productName?: string | null; quantity?: number | null; errors: string[];
  }[];
}
