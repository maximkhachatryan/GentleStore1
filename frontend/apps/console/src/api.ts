import { createApi, createHttp, tokenStore } from '@gentlestore/shared';
import { managedStore } from './auth/managedStore';

export const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';
export const STOREFRONT_URL = import.meta.env.VITE_STOREFRONT_URL ?? 'http://localhost:5174';

let unauthorizedHandler: (() => void) | undefined;

export function setOnUnauthorized(handler: () => void): void {
  unauthorizedHandler = handler;
}

export const http = createHttp(API_URL, () => unauthorizedHandler?.());

// Admins are not bound to a store, so scope backoffice requests to the store they are managing.
http.interceptors.request.use((config) => {
  const store = managedStore.get();
  if (store) config.headers.set('X-Store-Id', store.id);
  return config;
});

export const api = createApi(http);
export { tokenStore };
