export interface LoginRequest {
  email: string;
  password: string;
}

export type UserRole = 'SuperAdmin' | 'StoreOwner' | 'StoreStaff';

export interface UserDto {
  id: string;
  email: string;
  fullName: string;
  role: UserRole;
  storeId: string | null;
  storeName: string | null;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
  user: UserDto;
}

// ---- Admin ----
export interface StoreListItem {
  id: string;
  name: string;
  slug: string;
  phone: string;
  logoUrl: string | null;
  isActive: boolean;
  currency: string;
  productCount: number;
  categoryCount: number;
  createdAt: string;
}

export interface StoreDetail {
  id: string;
  name: string;
  slug: string;
  phone: string;
  logoUrl: string | null;
  description: string | null;
  currency: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateStoreRequest {
  name: string;
  slug?: string | null;
  phone: string;
  logoUrl?: string | null;
  description?: string | null;
  currency?: string | null;
  isActive?: boolean | null;
}

export interface UpdateStoreRequest {
  name: string;
  slug?: string | null;
  phone: string;
  logoUrl?: string | null;
  description?: string | null;
  currency: string;
  isActive: boolean;
}

export interface AdminUser {
  id: string;
  email: string;
  fullName: string;
  role: UserRole;
  storeId: string | null;
  storeName: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface CreateUserRequest {
  email: string;
  fullName: string;
  password: string;
  role: UserRole;
  storeId?: string | null;
}

export interface UpdateUserRequest {
  fullName: string;
  password?: string | null;
  isActive: boolean;
  role: UserRole;
  storeId?: string | null;
}

export interface AdminStats {
  storeCount: number;
  activeStoreCount: number;
  productCount: number;
  userCount: number;
}

// ---- Backoffice ----
export interface StoreProfile {
  id: string;
  name: string;
  slug: string;
  phone: string;
  logoUrl: string | null;
  description: string | null;
  currency: string;
  isActive: boolean;
}

export interface UpdateStoreProfileRequest {
  name: string;
  phone: string;
  logoUrl?: string | null;
  description?: string | null;
  currency: string;
}

export interface Category {
  id: string;
  name: string;
  displayOrder: number;
  productCount: number;
}

export interface CreateCategoryRequest {
  name: string;
  displayOrder: number;
}
export type UpdateCategoryRequest = CreateCategoryRequest;

export interface ProductImage {
  id: string;
  imageUrl: string;
  displayOrder: number;
}

export interface ProductTagRef {
  id: string;
  name: string;
}

export interface Product {
  id: string;
  categoryId: string;
  categoryName: string;
  name: string;
  description: string | null;
  price: number;
  isAvailable: boolean;
  displayOrder: number;
  images: ProductImage[];
  tags: ProductTagRef[];
  createdAt: string;
}

export interface CreateProductRequest {
  categoryId: string;
  name: string;
  description?: string | null;
  price: number;
  isAvailable: boolean;
  displayOrder: number;
  tagIds?: string[] | null;
}
export type UpdateProductRequest = CreateProductRequest;

export interface AddProductImageRequest {
  imageUrl: string;
  displayOrder?: number | null;
}

export interface Tag {
  id: string;
  name: string;
  displayOrder: number;
}

export interface CreateTagRequest {
  name: string;
  displayOrder: number;
}
export type UpdateTagRequest = CreateTagRequest;

// ---- Public ----
export interface PublicStoreListItem {
  slug: string;
  name: string;
  logoUrl: string | null;
  description: string | null;
  phone: string;
}

export interface PublicStore {
  slug: string;
  name: string;
  logoUrl: string | null;
  description: string | null;
  phone: string;
  currency: string;
}

export interface PublicCategory {
  id: string;
  name: string;
  displayOrder: number;
  productCount: number;
}

export interface PublicTag {
  id: string;
  name: string;
}

export interface PublicImage {
  imageUrl: string;
  displayOrder: number;
}

export interface PublicProductListItem {
  id: string;
  name: string;
  price: number;
  currency: string;
  inStock: boolean;
  primaryImageUrl: string | null;
  categoryId: string;
  tags: string[];
}

export interface PublicProduct {
  id: string;
  name: string;
  description: string | null;
  price: number;
  currency: string;
  inStock: boolean;
  categoryId: string;
  categoryName: string;
  images: PublicImage[];
  tags: PublicTag[];
}

export interface UploadResponse {
  url: string;
}

export interface ProductQuery {
  categoryId?: string;
  tagId?: string;
  search?: string;
}
