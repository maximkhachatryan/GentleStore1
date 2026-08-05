import type { AxiosInstance } from 'axios';
import type {
  AddProductImageRequest,
  AdminStats,
  AdminUser,
  Category,
  CreateCategoryRequest,
  CreateProductRequest,
  CreateStoreRequest,
  CreateTagRequest,
  CreateUserRequest,
  LoginRequest,
  LoginResponse,
  Product,
  ProductImage,
  ProductQuery,
  ProductVariant,
  CreateProductVariantRequest,
  UpdateProductVariantRequest,
  PublicCategory,
  PublicProduct,
  PublicProductListItem,
  PublicStore,
  PublicStoreListItem,
  PublicTag,
  StoreDetail,
  StoreListItem,
  StoreProfile,
  Tag,
  UpdateCategoryRequest,
  UpdateProductRequest,
  UpdateStoreProfileRequest,
  UpdateStoreRequest,
  UpdateTagRequest,
  UpdateUserRequest,
  UploadResponse,
  UserDto,
  VariantAttributeDefinition,
  VariantAttributeOption,
  CreateVariantAttributeDefinitionRequest,
  UpdateVariantAttributeDefinitionRequest,
  CreateVariantAttributeOptionRequest,
  UpdateVariantAttributeOptionRequest,
} from './types';

export function createApi(http: AxiosInstance) {
  return {
    auth: {
      login: (body: LoginRequest) => http.post<LoginResponse>('/api/auth/login', body).then((r) => r.data),
      me: () => http.get<UserDto>('/api/auth/me').then((r) => r.data),
    },
    admin: {
      stats: () => http.get<AdminStats>('/api/admin/stats').then((r) => r.data),
      listStores: () => http.get<StoreListItem[]>('/api/admin/stores').then((r) => r.data),
      getStore: (id: string) => http.get<StoreDetail>(`/api/admin/stores/${id}`).then((r) => r.data),
      createStore: (body: CreateStoreRequest) => http.post<StoreDetail>('/api/admin/stores', body).then((r) => r.data),
      updateStore: (id: string, body: UpdateStoreRequest) =>
        http.put<StoreDetail>(`/api/admin/stores/${id}`, body).then((r) => r.data),
      activateStore: (id: string) => http.post(`/api/admin/stores/${id}/activate`).then((r) => r.data),
      deactivateStore: (id: string) => http.post(`/api/admin/stores/${id}/deactivate`).then((r) => r.data),
      deleteStore: (id: string) => http.delete(`/api/admin/stores/${id}`).then((r) => r.data),
      listUsers: () => http.get<AdminUser[]>('/api/admin/users').then((r) => r.data),
      createUser: (body: CreateUserRequest) => http.post<AdminUser>('/api/admin/users', body).then((r) => r.data),
      updateUser: (id: string, body: UpdateUserRequest) =>
        http.put<AdminUser>(`/api/admin/users/${id}`, body).then((r) => r.data),
      deleteUser: (id: string) => http.delete(`/api/admin/users/${id}`).then((r) => r.data),
    },
    backoffice: {
      getStore: () => http.get<StoreProfile>('/api/backoffice/store').then((r) => r.data),
      updateStore: (body: UpdateStoreProfileRequest) =>
        http.put<StoreProfile>('/api/backoffice/store', body).then((r) => r.data),
      listCategories: () => http.get<Category[]>('/api/backoffice/categories').then((r) => r.data),
      createCategory: (body: CreateCategoryRequest) =>
        http.post<Category>('/api/backoffice/categories', body).then((r) => r.data),
      updateCategory: (id: string, body: UpdateCategoryRequest) =>
        http.put<Category>(`/api/backoffice/categories/${id}`, body).then((r) => r.data),
      deleteCategory: (id: string) => http.delete(`/api/backoffice/categories/${id}`).then((r) => r.data),
      listProducts: (query?: ProductQuery) =>
        http.get<Product[]>('/api/backoffice/products', { params: query }).then((r) => r.data),
      getProduct: (id: string) => http.get<Product>(`/api/backoffice/products/${id}`).then((r) => r.data),
      createProduct: (body: CreateProductRequest) =>
        http.post<Product>('/api/backoffice/products', body).then((r) => r.data),
      updateProduct: (id: string, body: UpdateProductRequest) =>
        http.put<Product>(`/api/backoffice/products/${id}`, body).then((r) => r.data),
      deleteProduct: (id: string) => http.delete(`/api/backoffice/products/${id}`).then((r) => r.data),
      addProductImage: (id: string, body: AddProductImageRequest) =>
        http.post<ProductImage>(`/api/backoffice/products/${id}/images`, body).then((r) => r.data),
      deleteProductImage: (id: string, imageId: string) =>
        http.delete(`/api/backoffice/products/${id}/images/${imageId}`).then((r) => r.data),
      listProductVariants: (productId: string) =>
        http.get<ProductVariant[]>(`/api/backoffice/products/${productId}/variants`).then((r) => r.data),
      createProductVariant: (productId: string, body: CreateProductVariantRequest) =>
        http.post<ProductVariant>(`/api/backoffice/products/${productId}/variants`, body).then((r) => r.data),
      updateProductVariant: (productId: string, variantId: string, body: UpdateProductVariantRequest) =>
        http.put<ProductVariant>(`/api/backoffice/products/${productId}/variants/${variantId}`, body).then((r) => r.data),
      deleteProductVariant: (productId: string, variantId: string) =>
        http.delete(`/api/backoffice/products/${productId}/variants/${variantId}`).then((r) => r.data),
      listTags: () => http.get<Tag[]>('/api/backoffice/tags').then((r) => r.data),
      createTag: (body: CreateTagRequest) => http.post<Tag>('/api/backoffice/tags', body).then((r) => r.data),
      updateTag: (id: string, body: UpdateTagRequest) =>
        http.put<Tag>(`/api/backoffice/tags/${id}`, body).then((r) => r.data),
      deleteTag: (id: string) => http.delete(`/api/backoffice/tags/${id}`).then((r) => r.data),
      listVariantAttributes: () =>
        http.get<VariantAttributeDefinition[]>('/api/backoffice/variant-attributes').then((r) => r.data),
      createVariantAttribute: (body: CreateVariantAttributeDefinitionRequest) =>
        http.post<VariantAttributeDefinition>('/api/backoffice/variant-attributes', body).then((r) => r.data),
      updateVariantAttribute: (id: string, body: UpdateVariantAttributeDefinitionRequest) =>
        http.put<VariantAttributeDefinition>(`/api/backoffice/variant-attributes/${id}`, body).then((r) => r.data),
      deleteVariantAttribute: (id: string) =>
        http.delete(`/api/backoffice/variant-attributes/${id}`).then((r) => r.data),
      addVariantAttributeOption: (id: string, body: CreateVariantAttributeOptionRequest) =>
        http.post<VariantAttributeOption>(`/api/backoffice/variant-attributes/${id}/options`, body).then((r) => r.data),
      updateVariantAttributeOption: (id: string, optionId: string, body: UpdateVariantAttributeOptionRequest) =>
        http.put<VariantAttributeOption>(`/api/backoffice/variant-attributes/${id}/options/${optionId}`, body).then((r) => r.data),
      deleteVariantAttributeOption: (id: string, optionId: string) =>
        http.delete(`/api/backoffice/variant-attributes/${id}/options/${optionId}`).then((r) => r.data),
    },
    store: {
      list: (search?: string) =>
        http.get<PublicStoreListItem[]>('/api/public/stores', { params: { search } }).then((r) => r.data),
      get: (slug: string) => http.get<PublicStore>(`/api/public/stores/${slug}`).then((r) => r.data),
      categories: (slug: string) =>
        http.get<PublicCategory[]>(`/api/public/stores/${slug}/categories`).then((r) => r.data),
      tags: (slug: string) => http.get<PublicTag[]>(`/api/public/stores/${slug}/tags`).then((r) => r.data),
      products: (slug: string, query?: ProductQuery) =>
        http.get<PublicProductListItem[]>(`/api/public/stores/${slug}/products`, { params: query }).then((r) => r.data),
      product: (slug: string, id: string) =>
        http.get<PublicProduct>(`/api/public/stores/${slug}/products/${id}`).then((r) => r.data),
    },
    uploads: {
      upload: (file: File) => {
        const form = new FormData();
        form.append('file', file);
        return http.post<UploadResponse>('/api/uploads', form).then((r) => r.data);
      },
    },
  };
}

export type Api = ReturnType<typeof createApi>;
