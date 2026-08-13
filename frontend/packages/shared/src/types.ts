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
  storeSlug: string | null;
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

/** `Public`: anyone with the link. `InviteOnly`: only browsers that redeemed a personal invite. */
export type StorefrontAccessMode = 'Public' | 'InviteOnly';

export interface StoreProfile {
  id: string;
  name: string;
  slug: string;
  phone: string;
  logoUrl: string | null;
  description: string | null;
  currency: string;
  isActive: boolean;
  storefrontAccess: StorefrontAccessMode;
}

export interface UpdateStoreProfileRequest {
  name: string;
  phone: string;
  logoUrl?: string | null;
  description?: string | null;
  currency: string;
  storefrontAccess?: StorefrontAccessMode | null;
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

export interface VariantAttribute {
  definitionId: string | null;
  optionId: string | null;
  name: string;
  value: string;
}

export interface ProductVariant {
  id: string;
  sku: string | null;
  price: number;
  isAvailable: boolean;
  displayOrder: number;
  attributes: VariantAttribute[];
}

export interface CreateProductVariantRequest {
  sku?: string | null;
  price: number;
  isAvailable: boolean;
  displayOrder: number;
  optionIds: string[];
}
export type UpdateProductVariantRequest = CreateProductVariantRequest;

export interface Product {
  id: string;
  categoryId: string;
  categoryName: string;
  name: string;
  description: string | null;
  price: number | null;
  isAvailable: boolean;
  displayOrder: number;
  images: ProductImage[];
  tags: ProductTagRef[];
  variants: ProductVariant[];
  createdAt: string;
}

export interface CreateProductRequest {
  categoryId: string;
  name: string;
  description?: string | null;
  price?: number | null;
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

export interface VariantAttributeOption {
  id: string;
  value: string;
  displayOrder: number;
}

export interface VariantAttributeDefinition {
  id: string;
  name: string;
  displayOrder: number;
  options: VariantAttributeOption[];
}

export interface CreateVariantAttributeDefinitionRequest {
  name: string;
  displayOrder: number;
}
export type UpdateVariantAttributeDefinitionRequest = CreateVariantAttributeDefinitionRequest;

export interface CreateVariantAttributeOptionRequest {
  value: string;
  displayOrder: number;
}
export type UpdateVariantAttributeOptionRequest = CreateVariantAttributeOptionRequest;

// ---- Storefront customers ----

/**
 * Derived server-side from invites and sessions:
 * `new` (never invited), `invited` (link outstanding), `active` (at least one signed-in browser),
 * `expired` (link lapsed unused), `blocked`.
 */
export type CustomerStatus = 'new' | 'invited' | 'active' | 'expired' | 'blocked';

export interface Customer {
  id: string;
  /** As the store typed it, for display. */
  phone: string;
  /** Digits only — what wa.me links need. */
  phoneNormalized: string;
  fullName: string | null;
  note: string | null;
  isBlocked: boolean;
  status: CustomerStatus;
  activeDeviceCount: number;
  pendingInviteExpiresAt: string | null;
  lastSeenAt: string | null;
  firstActivatedAt: string | null;
  createdAt: string;
}

export interface CreateCustomerRequest {
  phone: string;
  fullName?: string | null;
  note?: string | null;
}
export type UpdateCustomerRequest = CreateCustomerRequest;

export interface CustomerQuery {
  search?: string;
  status?: CustomerStatus;
}

/** Returned once, when the invite is minted — the server only keeps a hash of the secret. */
export interface CustomerInviteLink {
  id: string;
  url: string;
  expiresAt: string;
}

export interface CustomerInvite {
  id: string;
  status: 'pending' | 'used' | 'revoked' | 'expired';
  createdAt: string;
  expiresAt: string;
  redeemedAt: string | null;
  revokedAt: string | null;
  redeemedUserAgent: string | null;
}

export interface CustomerDevice {
  id: string;
  createdAt: string;
  lastSeenAt: string;
  userAgent: string | null;
}

export interface CustomerDetail {
  customer: Customer;
  devices: CustomerDevice[];
  invites: CustomerInvite[];
}

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
  accessMode: StorefrontAccessMode;
  /** The customer this browser is signed in as; null on public storefronts. */
  visitor: StorefrontVisitor | null;
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

export interface PublicVariantAttribute {
  name: string;
  value: string;
}

export interface PublicVariant {
  id: string;
  sku: string | null;
  price: number;
  inStock: boolean;
  attributes: PublicVariantAttribute[];
}

export interface PublicProductListItem {
  id: string;
  name: string;
  price: number | null;
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
  price: number | null;
  currency: string;
  inStock: boolean;
  categoryId: string;
  categoryName: string;
  images: PublicImage[];
  tags: PublicTag[];
  variants: PublicVariant[];
}

export interface StorefrontVisitor {
  displayName: string | null;
  /** Only the last digits, so a shared device does not leak a full number. */
  phoneMasked: string;
}

/** Gate status for a storefront — readable before the catalogue is unlocked. */
export interface StorefrontAccess {
  slug: string;
  name: string;
  logoUrl: string | null;
  phone: string;
  accessMode: StorefrontAccessMode;
  unlocked: boolean;
  visitor: StorefrontVisitor | null;
}

export type RedeemInviteStatus =
  | 'unlocked'
  | 'already_unlocked'
  | 'already_used'
  | 'expired'
  | 'revoked'
  | 'blocked'
  | 'invalid';

export interface RedeemInviteResult {
  status: RedeemInviteStatus;
  visitor: StorefrontVisitor | null;
}

export interface UploadResponse {
  url: string;
}

export interface ProductQuery {
  categoryId?: string;
  tagId?: string;
  search?: string;
}
