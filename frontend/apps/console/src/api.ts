import { createApi, createHttp, tokenStore } from '@gentlestore/shared';

export const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';

let unauthorizedHandler: (() => void) | undefined;

export function setOnUnauthorized(handler: () => void): void {
  unauthorizedHandler = handler;
}

export const http = createHttp(API_URL, () => unauthorizedHandler?.());
export const api = createApi(http);
export { tokenStore };
