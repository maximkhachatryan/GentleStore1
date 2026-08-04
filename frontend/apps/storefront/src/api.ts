import { createApi, createHttp } from '@gentlestore/shared';

export const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';

export const http = createHttp(API_URL);
export const api = createApi(http);
