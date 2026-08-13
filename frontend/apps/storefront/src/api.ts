import { createApi, createHttp } from '@gentlestore/shared';

export const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';

// withCredentials: the storefront's customer session lives in an HttpOnly cookie the API sets,
// which the browser only sends on credentialed cross-origin requests.
export const http = createHttp(API_URL, { withCredentials: true });
export const api = createApi(http);
