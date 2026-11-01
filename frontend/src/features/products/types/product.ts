export interface ProductSummary {
  productId: number; productCode: string; productName: string; model: string; unit: string;
  categoryId: number; categoryName: string; brandId: number; brandName: string; status: string;
}
export interface ProductDetail extends ProductSummary {
  specifications: string | null; lengthCm: number; widthCm: number; heightCm: number; grossWeightKg: number;
  suppliers: { supplierId: number; supplierName: string }[]; createdByUserId: number;
  createdAt: string; updatedAt: string | null; rowVersion: string;
}
export interface ProductPage { items: ProductSummary[]; totalCount: number; page: number; pageSize: number }
export interface ProductOptions {
  categories: { categoryId: number; categoryName: string }[];
  brands: { brandId: number; brandName: string }[];
  suppliers: { supplierId: number; supplierName: string }[];
}
export interface ProductInput {
  productCode: string; productName: string; model: string; unit: string;
  categoryId: number; brandId: number; status: string; specifications: string | null;
  lengthCm: number; widthCm: number; heightCm: number; grossWeightKg: number; supplierIds: number[]; rowVersion?: string;
}
