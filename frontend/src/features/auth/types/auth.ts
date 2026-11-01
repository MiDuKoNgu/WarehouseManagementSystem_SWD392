export type UserRole = 'ADMIN' | 'PURCHASING_STAFF' | 'WAREHOUSE_STAFF';
export interface UserProfile {
  userId: number;
  username: string;
  fullName: string;
  role: UserRole;
  warehouseId: number | null;
}
export const rolePaths: Record<UserRole, string> = {
  ADMIN: '/admin', PURCHASING_STAFF: '/purchasing', WAREHOUSE_STAFF: '/warehouse',
};
export const roleLabels: Record<UserRole, string> = {
  ADMIN: 'Quản trị viên', PURCHASING_STAFF: 'Nhân viên mua hàng', WAREHOUSE_STAFF: 'Nhân viên kho',
};
